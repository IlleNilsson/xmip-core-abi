// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

// xmip_event_test.cpp - include/xmip_event.hpp through the runtime's real
// library: a Subscription drains what it matches and frees its Batch, a
// listener is called back through a std::function, and an Event reaches
// its subscriber within a millisecond of its publish.
//
//     xmip_event_test <runtime library> <audit directory>
//
// Both default to XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY; the
// audit directory is never empty here. Exit 0 when every check is OK.

#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <cstdlib>
#include <mutex>
#include <thread>

#include "xmip_event.hpp"

namespace {

using namespace xmip::event;
using Clock = std::chrono::steady_clock;

constexpr std::string_view subscriber = "0198a3c4-0000-7000-8000-000000000042";
constexpr std::string_view program = "xmip-core-abi cpp test";
constexpr std::size_t warmup = 50;
constexpr std::size_t measured = 500;

int failures = 0;

void check(bool ok, const std::string &what) {
    std::printf("%s  %s\n", ok ? "OK    " : "FAILED", what.c_str());
    failures += ok ? 0 : 1;
}

Draft raised(std::string_view scope, XmipOutcome outcome) {
    Draft draft;
    draft.type = "se.xmip.receive.failure";
    draft.action = XMIP_ACTION_RECEIVE;
    draft.outcome = outcome;
    draft.scope = scope;
    draft.artifact = "orders";
    return draft;
}

// Arrivals: when each one was seen, and how many have been.
struct Timing {
    std::atomic<std::size_t> received{0};
    Clock::time_point at[warmup + measured];

    void arrived(Clock::time_point when) {
        std::size_t slot = received.load();
        if (slot < warmup + measured) {
            at[slot] = when;
        }
        received.fetch_add(1);
    }

    bool awaited(std::size_t i, Clock::time_point since) const {
        while (received.load() <= i) {
            if (Clock::now() - since > std::chrono::seconds(2)) {
                return false;
            }
            std::this_thread::yield();
        }
        return true;
    }
};

// Median, p99 and worst in microseconds, printed; the first two returned.
std::pair<double, double> spread(const std::string &what, const Clock::time_point *from,
                                 const Clock::time_point *to) {
    std::vector<double> micros;
    for (std::size_t i = warmup; i < warmup + measured; i++) {
        micros.push_back(std::chrono::duration<double, std::micro>(to[i] - from[i]).count());
    }
    std::sort(micros.begin(), micros.end());
    double median = micros[measured / 2];
    double p99 = micros[measured * 99 / 100];
    std::printf("        %-26s median %8.1f us, p99 %8.1f us, worst %8.1f us over %zu\n",
                what.c_str(), median, p99, micros.back(), measured);
    return {median, p99};
}

// Publish one Event and wait for it, then wake a plain thread on a plain
// condition variable and wait for that, round after round. The rule is a
// millisecond apart from load, so each bound is 1 ms plus the plain wake's.
void measure(const Library &xmip, Timing &timing, std::string_view scope, const char *path) {
    static Clock::time_point sent[warmup + measured];
    static Clock::time_point woken[warmup + measured];
    Timing plain;
    std::mutex lock;
    std::condition_variable ready;
    std::size_t posted = 0;
    std::thread waiter([&] {
        for (std::size_t seen = 0; seen < warmup + measured; seen++) {
            std::unique_lock<std::mutex> held(lock);
            ready.wait(held, [&] { return posted > seen; });
            held.unlock();
            plain.arrived(Clock::now());
        }
    });
    auto post = [&] {
        {
            std::lock_guard<std::mutex> held(lock);
            posted++;
        }
        ready.notify_one();
    };
    Draft draft = raised(scope, XMIP_OUTCOME_FAILURE);
    bool ok = true;
    for (std::size_t i = 0; ok && i < warmup + measured; i++) {
        sent[i] = Clock::now();
        ok = xmip.publish(draft) == 1 && timing.awaited(i, sent[i]);
        woken[i] = Clock::now();
        post();
        ok = ok && plain.awaited(i, woken[i]);
    }
    while (plain.received.load() < warmup + measured) {
        post();
    }
    waiter.join();
    if (!ok) {
        check(false, std::string(path) + ": every Event of the latency run arrived");
        return;
    }
    auto [median, p99] = spread(std::string("publish to ") + path, sent, timing.at);
    auto [usual, tail] = spread("a plain wake beside it", woken, plain.at);
    check(median < 1000.0 + usual, std::string(path) + ": median under 1 ms plus the plain's");
    check(p99 < 1000.0 + tail, std::string(path) + ": p99 under 1 ms plus the plain's");
}

void a_subscriber_is_a_party(const Library &xmip, const std::string &directory) {
    try {
        xmip.subscribe(program, directory, "", Filter{});
        check(false, "no subscriber is refused");
    } catch (const Error &error) {
        check(error.status() == XMIP_E_INVALID, "no subscriber is XMIP_E_INVALID");
    }
}

void a_subscription_drains_what_it_matches(const Library &xmip, const std::string &directory) {
    Filter filter;
    filter.outcomes = {XMIP_OUTCOME_FAILURE};
    filter.scope = "xmip:///cpp-drain";
    Subscription subscription = xmip.subscribe(program, directory, subscriber, filter);

    Draft draft = raised("xmip:///cpp-drain/node/n1/receive/orders", XMIP_OUTCOME_FAILURE);
    draft.diagnostics = {{"status", "refused"}};
    check(xmip.publish(draft) == 1, "a matching Event is delivered to one subscription");
    check(xmip.publish(raised("xmip:///cpp-drain/node/n1", XMIP_OUTCOME_SUCCESS)) == 0,
          "an Event of another outcome is delivered to none");

    std::optional<Batch> batch = subscription.next(1000);
    check(batch && batch->size() == 1 && batch->refused() == 0, "next hands over one Event");
    if (batch && batch->size() == 1) {
        EventView event = (*batch)[0];
        check(event.type() == "se.xmip.receive.failure", "its type");
        check(event.scope() == "xmip:///cpp-drain/node/n1/receive/orders", "its scope");
        check(event.artifact() == "orders", "its artifact");
        check(event.id().size() == 36, "its id was minted as a UUID");
        check(event.outcome() == XMIP_OUTCOME_FAILURE, "its outcome");
        check(event.diagnostics() == 1 && event.diagnostic(0).second == "refused",
              "its diagnostics");
    }
    batch.reset();
    check(!subscription.next(1), "an empty queue times out");
}

void a_drained_event_arrives_within_a_millisecond(const Library &xmip,
                                                  const std::string &directory) {
    Filter filter;
    filter.scope = "xmip:///cpp-drain-latency";
    Subscription subscription = xmip.subscribe(program, directory, subscriber, filter);
    static Timing timing;
    std::atomic<bool> stop{false};
    std::thread receiver([&] {
        while (!stop.load()) {
            if (std::optional<Batch> batch = subscription.next(100, 16)) {
                Clock::time_point when = Clock::now();
                for (std::size_t i = 0; i < batch->size(); i++) {
                    timing.arrived(when);
                }
            }
        }
    });
    measure(xmip, timing, "xmip:///cpp-drain-latency/node/n1", "next");
    stop.store(true);
    receiver.join();
}

void a_listener_is_called_back_within_a_millisecond(const Library &xmip,
                                                    const std::string &directory) {
    Filter filter;
    filter.scope = "xmip:///cpp-listen";
    static Timing timing;
    Subscription listening =
        xmip.listen(program, directory, subscriber, filter, [](const EventView &event) {
            if (event.type() == "se.xmip.receive.failure") {
                timing.arrived(Clock::now());
            }
        });
    try {
        (void)listening.next(1);
        check(false, "a listening subscription is refused a drain");
    } catch (const Error &error) {
        check(error.status() == XMIP_E_STATE, "a listening subscription is never drained");
    }
    measure(xmip, timing, "xmip:///cpp-listen/node/n1", "callback");
}

} // namespace

int main(int argc, char **argv) {
    const char *path = argc > 1 ? argv[1] : std::getenv("XMIP_RUNTIME_LIBRARY");
    const char *directory = argc > 2 ? argv[2] : std::getenv("XMIP_EVENT_AUDIT_DIRECTORY");
    if (!path || !directory || !*directory) {
        std::fprintf(stderr, "usage: xmip_event_test <runtime library> <audit directory>\n");
        return 2;
    }
    try {
        Library xmip(path);
        a_subscriber_is_a_party(xmip, directory);
        a_subscription_drains_what_it_matches(xmip, directory);
        a_drained_event_arrives_within_a_millisecond(xmip, directory);
        a_listener_is_called_back_within_a_millisecond(xmip, directory);
    } catch (const Error &error) {
        check(false, std::string("unexpected status: ") + error.what());
    }
    if (failures) {
        std::printf("FAILED. %d check(s) of the C++ binding\n", failures);
        return 1;
    }
    std::printf("OK. C++ binding\n");
    return 0;
}
