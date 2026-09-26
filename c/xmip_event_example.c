/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_event_example.c - subscribe to Xmip's Events from C (ADR-0065).
 *
 * Loads the runtime's library from the path given as the first argument, or
 * from XMIP_RUNTIME_LIBRARY; subscribes to the failures at one scope;
 * publishes one Event there; drains it, prints it, frees the batch and
 * unsubscribes. The audit directory is the second argument, or the folder
 * xmip-event-example in the working directory.
 */

#include <stdio.h>
#include <stdlib.h>

#include "xmip_event_library.h"

/* A party UUID for the subscriber: any valid one, in this process. */
#define SUBSCRIBER "0198a3c4-0000-7000-8000-000000000042"
#define SCOPE      "xmip:///example/node/n1/receive/orders"

static void print(const char *name, XmipStr value)
{
    printf("  %-9s %.*s\n", name, (int)value.len, (const char *)value.ptr);
}

int main(int argc, char **argv)
{
    const char *path = argc > 1 ? argv[1] : getenv("XMIP_RUNTIME_LIBRARY");
    const char *directory = argc > 2 ? argv[2] : "xmip-event-example";
    if (!path) {
        fprintf(stderr, "usage: xmip_event_example <runtime library> [audit directory]\n");
        return 2;
    }

    XmipEventLibrary xmip;
    if (xmip_event_library_open(path, &xmip) != XMIP_OK) {
        fprintf(stderr, "FAILED. %s does not load or lacks the event exports.\n", path);
        return 1;
    }

    /* Failures at or beneath the example's scope; empty lists are any. */
    const int32_t failures[] = {XMIP_OUTCOME_FAILURE};
    XmipEventFilter filter = {0};
    filter.outcomes = failures;
    filter.outcomes_len = 1;
    filter.scope = xmip_str("xmip:///example");

    XmipEventSubscription *subscription = NULL;
    uint8_t said[512];
    size_t said_len = 0;
    XmipStatus status = xmip.subscribe(xmip_str("xmip-event-example"), xmip_str(directory),
                                       xmip_str(SUBSCRIBER), &filter, 0, &subscription,
                                       said, sizeof said, &said_len);
    if (status != XMIP_OK) {
        fprintf(stderr, "REFUSED (%d). %.*s\n", status, (int)said_len, (const char *)said);
        xmip_event_library_close(&xmip);
        return 1;
    }

    /* An Event of our own: an empty id is minted, a time of 0 is now. */
    const XmipStr diagnostics[] = {xmip_str("status"), xmip_str("refused")};
    XmipEvent raised = {0};
    raised.type = xmip_str("se.xmip.receive.failure");
    raised.action = XMIP_ACTION_RECEIVE;
    raised.outcome = XMIP_OUTCOME_FAILURE;
    raised.scope = xmip_str(SCOPE);
    raised.artifact = xmip_str("orders");
    raised.diagnostics = diagnostics;
    raised.diagnostics_len = 2;
    size_t delivered = 0;
    status = xmip.publish(&raised, &delivered);
    printf("publish: %d, delivered to %zu subscription(s)\n", status, delivered);

    XmipEventBatch *batch = NULL;
    const XmipEvent *events = NULL;
    size_t len = 0;
    uint64_t refused = 0;
    status = xmip.next(subscription, 1000, 16, &batch, &events, &len, &refused);
    if (status == XMIP_OK) {
        for (size_t i = 0; i < len; i++) {
            printf("Event %zu of %zu\n", i + 1, len);
            print("id", events[i].id);
            print("type", events[i].type);
            print("scope", events[i].scope);
            print("artifact", events[i].artifact);
            for (size_t d = 0; d + 1 < events[i].diagnostics_len; d += 2) {
                print(".", events[i].diagnostics[d]);
                print("  =", events[i].diagnostics[d + 1]);
            }
        }
        xmip.batch_free(batch);
    }
    else {
        printf("next: %d (nothing arrived)\n", status);
    }

    xmip.unsubscribe(subscription);
    xmip_event_library_close(&xmip);
    return status == XMIP_OK ? 0 : 1;
}
