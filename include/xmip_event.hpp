// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

// xmip_event.hpp - xmip_operate.h section 11 for C++17: the runtime's
// library loaded once by path, an EventSubscription that unsubscribes when it
// goes, a Batch that frees when it goes, and a callback as a std::function.
//
// Header-only and thin (ADR-0065): which Events a filter matches, whether a
// subscriber may see them, the queue and the audit are the runtime's. An
// EventView's strings are std::string_view borrowing from the Batch it came
// in, or valid for the callback it was handed to. A Library outlives every
// EventSubscription it made.

#ifndef XMIP_EVENT_HPP
#define XMIP_EVENT_HPP

#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include "xmip_operate.h"

#ifdef _WIN32
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  include <windows.h>
#else
#  include <dlfcn.h>
#endif

namespace xmip::event {

// A status other than XMIP_OK, with what the runtime said about it.
class Error : public std::runtime_error {
public:
    Error(XmipStatus status, const std::string &said)
        : std::runtime_error(said.empty() ? "Xmip status " + std::to_string(status) : said),
          status_(status) {}

    XmipStatus status() const noexcept { return status_; }

private:
    XmipStatus status_;
};

inline XmipStr str(std::string_view text) noexcept {
    return XmipStr{reinterpret_cast<const uint8_t *>(text.data()), text.size()};
}

inline std::string_view view(XmipStr value) noexcept {
    return value.len ? std::string_view(reinterpret_cast<const char *>(value.ptr), value.len)
                     : std::string_view();
}

// One Event as the runtime handed it over; borrowed, never owned.
class EventView {
public:
    explicit EventView(const XmipEvent &event) noexcept : event_(&event) {}

    std::string_view id() const noexcept { return view(event_->id); }
    std::string_view type() const noexcept { return view(event_->type); }
    int64_t time_unix_nanos() const noexcept { return event_->time_unix_nanos; }
    XmipAction action() const noexcept { return static_cast<XmipAction>(event_->action); }
    XmipOutcome outcome() const noexcept { return static_cast<XmipOutcome>(event_->outcome); }
    std::string_view scope() const noexcept { return view(event_->scope); }
    std::string_view journey() const noexcept { return view(event_->journey); }
    std::string_view message() const noexcept { return view(event_->message); }
    std::string_view stream() const noexcept { return view(event_->stream); }
    std::string_view endpoint() const noexcept { return view(event_->endpoint); }
    std::string_view module() const noexcept { return view(event_->module); }
    std::string_view artifact() const noexcept { return view(event_->artifact); }
    std::string_view party() const noexcept { return view(event_->party); }

    // Diagnostics as name and value pairs.
    std::size_t diagnostics() const noexcept { return event_->diagnostics_len / 2; }
    std::pair<std::string_view, std::string_view> diagnostic(std::size_t index) const noexcept {
        return {view(event_->diagnostics[2 * index]), view(event_->diagnostics[2 * index + 1])};
    }

    const XmipEvent &raw() const noexcept { return *event_; }

private:
    const XmipEvent *event_;
};

// What a subscription asks for; every empty list or string is any.
struct Filter {
    std::vector<std::string> types;
    std::vector<XmipOutcome> outcomes;
    std::string scope;
    std::string party;
};

// An Event to publish. An empty id is minted and a time of 0 is now; the
// strings are borrowed for the publish call only.
struct Draft {
    std::string_view id;
    std::string_view type;
    int64_t time_unix_nanos = 0;
    XmipAction action = XMIP_ACTION_RECEIVE;
    XmipOutcome outcome = XMIP_OUTCOME_SUCCESS;
    std::string_view scope;
    std::string_view journey;
    std::string_view message;
    std::string_view stream;
    std::string_view endpoint;
    std::string_view module;
    std::string_view artifact;
    std::string_view party;
    std::vector<std::pair<std::string_view, std::string_view>> diagnostics;
};

// The symbols of section 11 a subscriber calls, resolved once.
struct Symbols {
    XmipEventSubscribeFn subscribe = nullptr;
    XmipEventNextFn next = nullptr;
    XmipEventBatchFreeFn batch_free = nullptr;
    XmipEventListenFn listen = nullptr;
    XmipEventUnsubscribeFn unsubscribe = nullptr;
    XmipEventPublishFn publish = nullptr;
    XmipEventAuthorizeFn authorize = nullptr;
    XmipEventBatchUnheardFn batch_unheard = nullptr;
    XmipEventUnheardFn unheard = nullptr;
};

// What a call that writes JSON into a buffer wrote, asked again with room
// where the first buffer was too small.
template <typename Write> std::string written(Write write) {
    std::string out(4096, '\0');
    for (;;) {
        std::size_t length = 0;
        XmipStatus status =
            write(reinterpret_cast<uint8_t *>(out.data()), out.size(), &length);
        if (status != XMIP_OK) {
            throw Error(status, "");
        }
        if (length <= out.size()) {
            out.resize(length);
            return out;
        }
        out.assign(length, '\0');
    }
}

// A drained batch; frees itself, and every EventView of it with it.
class Batch {
public:
    Batch(const Symbols *symbols, XmipEventBatch *batch, const XmipEvent *events,
          std::size_t size, uint64_t refused) noexcept
        : symbols_(symbols), batch_(batch), events_(events), size_(size), refused_(refused) {}
    Batch(const Batch &) = delete;
    Batch &operator=(const Batch &) = delete;
    Batch(Batch &&other) noexcept { *this = std::move(other); }
    Batch &operator=(Batch &&other) noexcept {
        if (this != &other) {
            release();
            symbols_ = other.symbols_;
            batch_ = std::exchange(other.batch_, nullptr);
            events_ = other.events_;
            size_ = std::exchange(other.size_, 0);
            refused_ = other.refused_;
        }
        return *this;
    }
    ~Batch() { release(); }

    std::size_t size() const noexcept { return size_; }
    EventView operator[](std::size_t index) const noexcept { return EventView(events_[index]); }
    // How many Events a full queue refused since the last drain.
    uint64_t refused() const noexcept { return refused_; }
    // Who was not heard when it was drained, as the header writes it:
    // {"changed":..,"unheard":[{"by","node","since_unix_nanos","why","said"}]}.
    // A member of the cluster whose Events are not among any delivered.
    std::string unheard() const {
        return written([this](uint8_t *out, std::size_t cap, std::size_t *length) {
            return symbols_->batch_unheard(batch_, out, cap, length);
        });
    }

private:
    void release() noexcept {
        if (batch_) {
            symbols_->batch_free(std::exchange(batch_, nullptr));
        }
    }

    const Symbols *symbols_ = nullptr;
    XmipEventBatch *batch_ = nullptr;
    const XmipEvent *events_ = nullptr;
    std::size_t size_ = 0;
    uint64_t refused_ = 0;
};

using Callback = std::function<void(const EventView &)>;

// The program's policy of who may subscribe: the attempt's Party, mechanism,
// value, scope and Event type, answered with an XmipEventDecision.
using Decide = std::function<int32_t(std::string_view, std::string_view, std::string_view,
                                     std::string_view, std::string_view)>;

// A subscription, drained or listening; unsubscribes when it goes.
class EventSubscription {
public:
    EventSubscription(const Symbols *symbols, XmipEventSubscription *handle,
                 std::unique_ptr<Callback> callback) noexcept
        : symbols_(symbols), handle_(handle), callback_(std::move(callback)) {}
    EventSubscription(const EventSubscription &) = delete;
    EventSubscription &operator=(const EventSubscription &) = delete;
    EventSubscription(EventSubscription &&other) noexcept { *this = std::move(other); }
    EventSubscription &operator=(EventSubscription &&other) noexcept {
        if (this != &other) {
            release();
            symbols_ = other.symbols_;
            handle_ = std::exchange(other.handle_, nullptr);
            callback_ = std::move(other.callback_);
        }
        return *this;
    }
    ~EventSubscription() { release(); }

    // Up to max Events, waiting up to timeout_ms for the first; nothing
    // when none arrived. Throws Error on any other status.
    std::optional<Batch> next(uint32_t timeout_ms, std::size_t max = 64) const {
        XmipEventBatch *batch = nullptr;
        const XmipEvent *events = nullptr;
        std::size_t len = 0;
        uint64_t refused = 0;
        XmipStatus status =
            symbols_->next(handle_, timeout_ms, max, &batch, &events, &len, &refused);
        if (status == XMIP_E_TIMEOUT) {
            return std::nullopt;
        }
        if (status != XMIP_OK) {
            throw Error(status, "");
        }
        return Batch(symbols_, batch, events, len, refused);
    }

private:
    void release() noexcept {
        if (handle_) {
            symbols_->unsubscribe(std::exchange(handle_, nullptr));
        }
    }

    const Symbols *symbols_ = nullptr;
    XmipEventSubscription *handle_ = nullptr;
    std::unique_ptr<Callback> callback_;
};

// The runtime's library, loaded by path; symbols resolved once.
class Library {
public:
    explicit Library(const std::string &path) {
#ifdef _WIN32
        handle_ = reinterpret_cast<void *>(LoadLibraryA(path.c_str()));
#else
        handle_ = dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL);
#endif
        if (!handle_) {
            throw Error(XMIP_E_NOT_FOUND, "cannot load " + path);
        }
        resolve(symbols_.subscribe, XMIP_EVENT_SUBSCRIBE_ENTRYPOINT);
        resolve(symbols_.next, XMIP_EVENT_NEXT_ENTRYPOINT);
        resolve(symbols_.batch_free, XMIP_EVENT_BATCH_FREE_ENTRYPOINT);
        resolve(symbols_.listen, XMIP_EVENT_LISTEN_ENTRYPOINT);
        resolve(symbols_.unsubscribe, XMIP_EVENT_UNSUBSCRIBE_ENTRYPOINT);
        resolve(symbols_.publish, XMIP_EVENT_PUBLISH_ENTRYPOINT);
        resolve(symbols_.authorize, XMIP_EVENT_AUTHORIZE_ENTRYPOINT);
        resolve(symbols_.batch_unheard, XMIP_EVENT_BATCH_UNHEARD_ENTRYPOINT);
        resolve(symbols_.unheard, XMIP_EVENT_UNHEARD_ENTRYPOINT);
    }
    Library(const Library &) = delete;
    Library &operator=(const Library &) = delete;
    ~Library() { close(); }

    // Subscribe to drain. Throws Error, with the gate's sentence when refused.
    EventSubscription subscribe(std::string_view program, std::string_view directory,
                           std::string_view subscriber, const Filter &filter,
                           std::size_t capacity = 0) const {
        return open(program, directory, subscriber, filter, capacity, nullptr);
    }

    // Subscribe to be called back on a runtime thread, one call at a time.
    EventSubscription listen(std::string_view program, std::string_view directory,
                        std::string_view subscriber, const Filter &filter, Callback callback,
                        std::size_t capacity = 0) const {
        return open(program, directory, subscriber, filter, capacity,
                    std::make_unique<Callback>(std::move(callback)));
    }

    // Hand this process's hub the program's policy of who may subscribe:
    // decide(party, mechanism, value, scope, type) answers XMIP_EVENT_ALLOW,
    // XMIP_EVENT_DENY or XMIP_EVENT_NO_OPINION, and nothing having an
    // opinion is a refusal. It replaces the policy handed before; being in
    // the process admits nobody. The policy lives as long as this Library.
    void authorize_by(Decide decide) {
        auto held = std::make_unique<Decide>(std::move(decide));
        XmipStatus status = symbols_.authorize(&Library::decided, held.get());
        if (status != XMIP_OK) {
            throw Error(status, "");
        }
        policy_ = std::move(held);
    }

    // Who this process's hub does not hear now, as the header writes it:
    // {"unheard":[...]}. What a listening subscription asks.
    std::string unheard() const {
        return written([this](uint8_t *out, std::size_t cap, std::size_t *length) {
            return symbols_.unheard(out, cap, length);
        });
    }

    // Hand an Event to every matching subscription; how many queues took it.
    std::size_t publish(const Draft &draft) const {
        std::vector<XmipStr> diagnostics;
        diagnostics.reserve(draft.diagnostics.size() * 2);
        for (const auto &[name, value] : draft.diagnostics) {
            diagnostics.push_back(str(name));
            diagnostics.push_back(str(value));
        }
        XmipEvent event{};
        event.id = str(draft.id);
        event.type = str(draft.type);
        event.time_unix_nanos = draft.time_unix_nanos;
        event.action = draft.action;
        event.outcome = draft.outcome;
        event.scope = str(draft.scope);
        event.journey = str(draft.journey);
        event.message = str(draft.message);
        event.stream = str(draft.stream);
        event.endpoint = str(draft.endpoint);
        event.module = str(draft.module);
        event.artifact = str(draft.artifact);
        event.party = str(draft.party);
        event.diagnostics = diagnostics.data();
        event.diagnostics_len = diagnostics.size();
        std::size_t delivered = 0;
        XmipStatus status = symbols_.publish(&event, &delivered);
        if (status != XMIP_OK) {
            throw Error(status, "");
        }
        return delivered;
    }

    const Symbols &symbols() const noexcept { return symbols_; }

private:
    template <typename Fn> void resolve(Fn &out, const char *name) {
#ifdef _WIN32
        auto found = reinterpret_cast<void *>(GetProcAddress(static_cast<HMODULE>(handle_), name));
#else
        void *found = dlsym(handle_, name);
#endif
        if (!found) {
            close();
            throw Error(XMIP_E_NOT_FOUND, std::string("the library has no ") + name);
        }
        out = reinterpret_cast<Fn>(found);
    }

    void close() noexcept {
        if (handle_) {
#ifdef _WIN32
            FreeLibrary(static_cast<HMODULE>(handle_));
#else
            dlclose(handle_);
#endif
            handle_ = nullptr;
        }
    }

    static int32_t decided(void *context, XmipStr party, XmipStr mechanism, XmipStr value,
                           XmipScope scope, XmipStr type) {
        try {
            return (*static_cast<Decide *>(context))(view(party), view(mechanism), view(value),
                                                     view(scope), view(type));
        } catch (...) {
            return XMIP_EVENT_NO_OPINION;
        }
    }

    static std::string_view view(XmipStr text) {
        return std::string_view(reinterpret_cast<const char *>(text.ptr), text.len);
    }

    static void trampoline(void *context, const XmipEvent *event) {
        (*static_cast<Callback *>(context))(EventView(*event));
    }

    EventSubscription open(std::string_view program, std::string_view directory,
                      std::string_view subscriber, const Filter &filter,
                      std::size_t capacity, std::unique_ptr<Callback> callback) const {
        std::vector<XmipStr> types;
        for (const auto &type : filter.types) {
            types.push_back(str(type));
        }
        std::vector<int32_t> outcomes(filter.outcomes.begin(), filter.outcomes.end());
        XmipEventFilter wire{};
        wire.types = types.data();
        wire.types_len = types.size();
        wire.outcomes = outcomes.data();
        wire.outcomes_len = outcomes.size();
        wire.scope = str(filter.scope);
        wire.party = str(filter.party);

        XmipEventSubscription *handle = nullptr;
        uint8_t said[512];
        std::size_t said_len = 0;
        XmipStatus status = callback
            ? symbols_.listen(str(program), str(directory), str(subscriber), &wire, capacity,
                              &Library::trampoline, callback.get(), &handle, said,
                              sizeof said, &said_len)
            : symbols_.subscribe(str(program), str(directory), str(subscriber), &wire,
                                 capacity, &handle, said, sizeof said, &said_len);
        if (status != XMIP_OK) {
            throw Error(status, std::string(reinterpret_cast<const char *>(said), said_len));
        }
        return EventSubscription(&symbols_, handle, std::move(callback));
    }

    std::unique_ptr<Decide> policy_;
    void *handle_ = nullptr;
    Symbols symbols_;
};

} // namespace xmip::event

#endif // XMIP_EVENT_HPP
