/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_event_test.c - xmip_operate.h section 11 through the runtime's real
 * library, from C: a subscription drains what it matches and nothing else,
 * a listener is called back, and an Event reaches its subscriber within a
 * millisecond of its publish (CONTRIBUTING, the millisecond rule).
 *
 *     xmip_event_test <runtime library> <audit directory>
 *
 * Both default to XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY. The
 * audit directory is never empty here: an empty one would audit to the
 * operating system's log. Exit 0 when every check is OK.
 */

#ifndef _WIN32
#  define _POSIX_C_SOURCE 200809L   /* clock_gettime under -std=c11 */
#endif

#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>

#include "xmip_event_library.h"

#ifdef _WIN32
#  include <process.h>
#else
#  include <pthread.h>
#  include <sched.h>
#  include <time.h>
#  include <unistd.h>
#endif

#define SUBSCRIBER "0198a3c4-0000-7000-8000-000000000042"
#define PROGRAM    "xmip-core-abi c test"
#define WARMUP     50
#define MEASURED   500

static XmipEventLibrary xmip;
static const char *directory;
static int failures;

static void check(int ok, const char *what)
{
    printf("%s  %s\n", ok ? "OK    " : "FAILED", what);
    if (!ok) {
        failures++;
    }
}

static int same(XmipStr value, const char *text)
{
    size_t len = strlen(text);
    return value.len == len && (len == 0 || memcmp(value.ptr, text, len) == 0);
}

static int64_t now_ns(void)
{
#ifdef _WIN32
    static LARGE_INTEGER frequency;
    LARGE_INTEGER counter;
    if (!frequency.QuadPart) {
        QueryPerformanceFrequency(&frequency);
    }
    QueryPerformanceCounter(&counter);
    return (int64_t)((double)counter.QuadPart * 1e9 / (double)frequency.QuadPart);
#else
    struct timespec at;
    clock_gettime(CLOCK_MONOTONIC, &at);
    return (int64_t)at.tv_sec * 1000000000 + at.tv_nsec;
#endif
}

static void relax(void)
{
#ifdef _WIN32
    SwitchToThread();
#else
    sched_yield();
#endif
}

typedef void (*Body)(void *);

typedef struct {
    Body  body;
    void *argument;
#ifdef _WIN32
    HANDLE handle;
#else
    pthread_t handle;
#endif
} Thread;

#ifdef _WIN32
static unsigned __stdcall run(void *thread)
{
    ((Thread *)thread)->body(((Thread *)thread)->argument);
    return 0;
}
#else
static void *run(void *thread)
{
    ((Thread *)thread)->body(((Thread *)thread)->argument);
    return NULL;
}
#endif

static void start(Thread *thread, Body body, void *argument)
{
    thread->body = body;
    thread->argument = argument;
#ifdef _WIN32
    thread->handle = (HANDLE)_beginthreadex(NULL, 0, run, thread, 0, NULL);
#else
    pthread_create(&thread->handle, NULL, run, thread);
#endif
}

static void join(Thread *thread)
{
#ifdef _WIN32
    WaitForSingleObject(thread->handle, INFINITE);
    CloseHandle(thread->handle);
#else
    pthread_join(thread->handle, NULL);
#endif
}

/* A filter over one scope, and optionally one outcome. */
static XmipEventFilter filter_at(const char *scope, const int32_t *outcome)
{
    XmipEventFilter filter = {0};
    filter.outcomes = outcome;
    filter.outcomes_len = outcome ? 1 : 0;
    filter.scope = xmip_str(scope);
    return filter;
}

static XmipEvent raised_at(const char *scope, int32_t outcome)
{
    XmipEvent event = {0};
    event.type = xmip_str("se.xmip.receive.failure");
    event.action = XMIP_ACTION_RECEIVE;
    event.outcome = outcome;
    event.scope = xmip_str(scope);
    event.artifact = xmip_str("orders");
    return event;
}

static XmipEventSubscription *subscribe(const XmipEventFilter *filter)
{
    XmipEventSubscription *out = NULL;
    uint8_t said[256];
    size_t said_len = 0;
    XmipStatus status = xmip.subscribe(xmip_str(PROGRAM), xmip_str(directory),
                                       xmip_str(SUBSCRIBER), filter, 0, &out, said,
                                       sizeof said, &said_len);
    check(status == XMIP_OK && out != NULL, "subscribe is OK");
    if (status != XMIP_OK) {
        printf("        said: %.*s\n", (int)said_len, (const char *)said);
    }
    return out;
}

static int compare(const void *left, const void *right)
{
    int64_t a = *(const int64_t *)left;
    int64_t b = *(const int64_t *)right;
    return (a > b) - (a < b);
}

/* Median, p99 and worst in microseconds, printed; the first two returned. */
static void spread(const char *what, int64_t *latency, double *median, double *p99)
{
    qsort(latency, MEASURED, sizeof *latency, compare);
    *median = (double)latency[MEASURED / 2] / 1000.0;
    *p99 = (double)latency[MEASURED * 99 / 100] / 1000.0;
    printf("        %-26s median %8.1f us, p99 %8.1f us, worst %8.1f us over %d\n", what,
           *median, *p99, (double)latency[MEASURED - 1] / 1000.0, MEASURED);
}

/* Arrivals: when each one was seen, and how many have been. */
typedef struct {
    atomic_size_t received;
    int64_t       at[WARMUP + MEASURED];
    XmipEventSubscription *subscription;
    atomic_int    stop;
} Timing;

static void arrived(Timing *timing, int64_t at)
{
    size_t slot = atomic_load(&timing->received);
    if (slot < WARMUP + MEASURED) {
        timing->at[slot] = at;
    }
    atomic_fetch_add(&timing->received, 1);
}

static int awaited(Timing *timing, size_t i, int64_t since)
{
    while (atomic_load(&timing->received) <= i) {
        if (now_ns() - since > 2000000000) {
            check(0, "an arrival within two seconds");
            return 0;
        }
        relax();
    }
    return 1;
}

/*
 * The control: a thread woken on a plain condition variable, beside the
 * Events and under the same load. The rule is a millisecond apart from
 * load, so each bound is 1 ms plus what the machine takes for that wake.
 */
typedef struct {
    Timing timing;
    size_t posted;
#ifdef _WIN32
    SRWLOCK            lock;
    CONDITION_VARIABLE ready;
#else
    pthread_mutex_t lock;
    pthread_cond_t  ready;
#endif
} Control;

static Control control;

static void control_lock(void)
{
#ifdef _WIN32
    AcquireSRWLockExclusive(&control.lock);
#else
    pthread_mutex_lock(&control.lock);
#endif
}

static void control_unlock(void)
{
#ifdef _WIN32
    ReleaseSRWLockExclusive(&control.lock);
#else
    pthread_mutex_unlock(&control.lock);
#endif
}

static void control_wait(void *unused)
{
    (void)unused;
    size_t seen = 0;
    while (seen < WARMUP + MEASURED) {
        control_lock();
        while (control.posted == seen) {
#ifdef _WIN32
            SleepConditionVariableSRW(&control.ready, &control.lock, INFINITE, 0);
#else
            pthread_cond_wait(&control.ready, &control.lock);
#endif
        }
        seen++;
        control_unlock();
        arrived(&control.timing, now_ns());
    }
}

static void control_post(void)
{
    control_lock();
    control.posted++;
    control_unlock();
#ifdef _WIN32
    WakeConditionVariable(&control.ready);
#else
    pthread_cond_signal(&control.ready);
#endif
}

/*
 * Publish one Event and wait for it, then wake the control and wait for
 * it, round after round; judge the Events against the control.
 */
static void measure(Timing *timing, const char *scope, const char *path)
{
    static int64_t sent[WARMUP + MEASURED];
    static int64_t woken[WARMUP + MEASURED];
    memset(&control, 0, sizeof control);
#ifdef _WIN32
    InitializeSRWLock(&control.lock);
    InitializeConditionVariable(&control.ready);
#else
    pthread_mutex_init(&control.lock, NULL);
    pthread_cond_init(&control.ready, NULL);
#endif
    Thread waiter;
    start(&waiter, control_wait, NULL);
    XmipEvent event = raised_at(scope, XMIP_OUTCOME_FAILURE);
    size_t delivered = 0;
    int ok = 1;
    for (size_t i = 0; ok && i < WARMUP + MEASURED; i++) {
        sent[i] = now_ns();
        ok = xmip.publish(&event, &delivered) == XMIP_OK && delivered == 1
            && awaited(timing, i, sent[i]);
        woken[i] = now_ns();
        control_post();
        ok = ok && awaited(&control.timing, i, woken[i]);
    }
    while (!ok && atomic_load(&control.timing.received) < WARMUP + MEASURED) {
        control_post();
        relax();
    }
    join(&waiter);
    if (!ok) {
        check(0, "every Event of the latency run arrived");
        return;
    }
    int64_t latency[MEASURED], plain[MEASURED];
    for (size_t i = 0; i < MEASURED; i++) {
        latency[i] = timing->at[WARMUP + i] - sent[WARMUP + i];
        plain[i] = control.timing.at[WARMUP + i] - woken[WARMUP + i];
    }
    double median, p99, usual, tail;
    char what[128];
    snprintf(what, sizeof what, "publish to %s", path);
    spread(what, latency, &median, &p99);
    spread("a plain wake beside it", plain, &usual, &tail);
    snprintf(what, sizeof what, "%s: median under 1 ms plus the plain wake's", path);
    check(median < 1000.0 + usual, what);
    snprintf(what, sizeof what, "%s: p99 under 1 ms plus the plain wake's", path);
    check(p99 < 1000.0 + tail, what);
}

static void drain(void *argument)
{
    Timing *timing = argument;
    while (!atomic_load(&timing->stop)) {
        XmipEventBatch *batch = NULL;
        const XmipEvent *events = NULL;
        size_t len = 0;
        uint64_t refused = 0;
        if (xmip.next(timing->subscription, 100, 16, &batch, &events, &len, &refused)
            == XMIP_OK) {
            int64_t at = now_ns();
            for (size_t i = 0; i < len; i++) {
                arrived(timing, at);
            }
            xmip.batch_free(batch);
        }
    }
}

static void told(void *context, const XmipEvent *event)
{
    int64_t at = now_ns();
    if (same(event->type, "se.xmip.receive.failure")) {
        arrived(context, at);
    }
}

static void a_subscription_drains_what_it_matches(void)
{
    const int32_t failure = XMIP_OUTCOME_FAILURE;
    XmipEventFilter filter = filter_at("xmip:///c-drain", &failure);
    XmipEventSubscription *subscription = subscribe(&filter);
    if (!subscription) {
        return;
    }
    const XmipStr diagnostics[] = {xmip_str("status"), xmip_str("refused")};
    XmipEvent event = raised_at("xmip:///c-drain/node/n1/receive/orders", failure);
    event.diagnostics = diagnostics;
    event.diagnostics_len = 2;
    size_t delivered = 0;
    check(xmip.publish(&event, &delivered) == XMIP_OK && delivered == 1,
          "a matching Event is delivered to one subscription");
    XmipEvent other = raised_at("xmip:///c-drain/node/n1", XMIP_OUTCOME_SUCCESS);
    check(xmip.publish(&other, &delivered) == XMIP_OK && delivered == 0,
          "an Event of another outcome is delivered to none");

    XmipEventBatch *batch = NULL;
    const XmipEvent *events = NULL;
    size_t len = 0;
    uint64_t refused = 0;
    XmipStatus status = xmip.next(subscription, 1000, 16, &batch, &events, &len, &refused);
    check(status == XMIP_OK && len == 1 && refused == 0, "next hands over the one Event");
    if (status == XMIP_OK && len == 1) {
        check(same(events[0].type, "se.xmip.receive.failure"), "its type");
        check(same(events[0].scope, "xmip:///c-drain/node/n1/receive/orders"), "its scope");
        check(same(events[0].artifact, "orders"), "its artifact");
        check(events[0].id.len == 36, "its id was minted as a UUID");
        check(events[0].time_unix_nanos > 0, "its time was set");
        check(events[0].outcome == XMIP_OUTCOME_FAILURE, "its outcome");
        check(events[0].diagnostics_len == 2 && same(events[0].diagnostics[1], "refused"),
              "its diagnostics");
    }
    xmip.batch_free(batch);

    status = xmip.next(subscription, 1, 16, &batch, &events, &len, &refused);
    check(status == XMIP_E_TIMEOUT && batch == NULL, "an empty queue times out, no batch");
    xmip.unsubscribe(subscription);
}

static void a_drained_event_arrives_within_a_millisecond(void)
{
    static Timing timing;
    XmipEventFilter filter = filter_at("xmip:///c-drain-latency", NULL);
    timing.subscription = subscribe(&filter);
    if (!timing.subscription) {
        return;
    }
    Thread receiver;
    start(&receiver, drain, &timing);
    measure(&timing, "xmip:///c-drain-latency/node/n1", "next");
    atomic_store(&timing.stop, 1);
    join(&receiver);
    xmip.unsubscribe(timing.subscription);
}

static void a_listener_is_called_back_within_a_millisecond(void)
{
    static Timing timing;
    XmipEventFilter filter = filter_at("xmip:///c-listen", NULL);
    XmipEventSubscription *out = NULL;
    uint8_t said[256];
    size_t said_len = 0;
    XmipStatus status = xmip.listen(xmip_str(PROGRAM), xmip_str(directory),
                                    xmip_str(SUBSCRIBER), &filter, 0, told, &timing, &out,
                                    said, sizeof said, &said_len);
    check(status == XMIP_OK && out != NULL, "listen is OK");
    if (status != XMIP_OK) {
        return;
    }
    XmipEventBatch *batch = NULL;
    const XmipEvent *events = NULL;
    size_t len = 0;
    uint64_t refused = 0;
    check(xmip.next(out, 1, 1, &batch, &events, &len, &refused) == XMIP_E_STATE,
          "a listening subscription is never drained");
    measure(&timing, "xmip:///c-listen/node/n1", "callback");
    xmip.unsubscribe(out);
}

static void a_subscriber_is_a_party(void)
{
    XmipEventSubscription *out = NULL;
    uint8_t said[64];
    size_t said_len = 0;
    XmipStatus status = xmip.subscribe(xmip_str(PROGRAM), xmip_str(directory), xmip_str(""),
                                       NULL, 0, &out, said, sizeof said, &said_len);
    check(status == XMIP_E_INVALID && out == NULL, "no subscriber is XMIP_E_INVALID");
    status = xmip.subscribe(xmip_str(PROGRAM), xmip_str(directory), xmip_str("partner-x"),
                            NULL, 0, &out, said, sizeof said, &said_len);
    check(status == XMIP_E_MALFORMED && out == NULL, "a subscriber not a UUID is MALFORMED");
}

int main(int argc, char **argv)
{
    const char *path = argc > 1 ? argv[1] : getenv("XMIP_RUNTIME_LIBRARY");
    directory = argc > 2 ? argv[2] : getenv("XMIP_EVENT_AUDIT_DIRECTORY");
    if (!path || !directory || !*directory) {
        fprintf(stderr, "usage: xmip_event_test <runtime library> <audit directory>\n");
        return 2;
    }
    if (xmip_event_library_open(path, &xmip) != XMIP_OK) {
        fprintf(stderr, "FAILED. %s does not export section 11.\n", path);
        return 1;
    }
    a_subscriber_is_a_party();
    a_subscription_drains_what_it_matches();
    a_drained_event_arrives_within_a_millisecond();
    a_listener_is_called_back_within_a_millisecond();
    xmip_event_library_close(&xmip);
    if (failures) {
        printf("FAILED. %d check(s) of the C binding\n", failures);
        return 1;
    }
    printf("OK. C binding\n");
    return 0;
}
