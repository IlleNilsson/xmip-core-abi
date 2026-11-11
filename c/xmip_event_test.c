/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_event_test.c - xmip_operate.h section 11 through the runtime's real
 * library, from C: a program subscribing names no Party of its own. It is
 * the account the operating system says it runs as, resolved through the
 * Parties of the node serving in its process (ADR-0019, amendment
 * 2026-11-11); this test's process serves no node, so subscribe and listen
 * are refused XMIP_E_AUTH with the sentence, and publish, the policy and
 * who is unheard still cross.
 *
 *     xmip_event_test <runtime library> <audit directory>
 *
 * Both default to XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY. The
 * audit directory is never empty here: an empty one would audit to the
 * operating system's log. Exit 0 when every check is OK.
 */

#include <stdio.h>
#include <stdlib.h>

#include "xmip_event_library.h"

#define PROGRAM "xmip-core-abi c test"
#define NO_PARTY "is no Party of a node serving in this process"

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

/* Whether the said bytes hold text. */
static int says(const uint8_t *said, size_t len, const char *text)
{
    size_t want = strlen(text);
    for (size_t at = 0; at + want <= len; at++) {
        if (memcmp(said + at, text, want) == 0) {
            return 1;
        }
    }
    return 0;
}

static void told(void *context, const XmipEvent *event)
{
    (void)context;
    (void)event;
}

/* The test's policy: every Party; a subscriber must still prove one. */
static int32_t allowed(void *context, XmipStr party, XmipStr mechanism, XmipStr value,
                       XmipScope artifact, XmipStr contract)
{
    (void)context;
    (void)party;
    (void)mechanism;
    (void)value;
    (void)artifact;
    (void)contract;
    return XMIP_EVENT_ALLOW;
}

static void a_program_subscribing_names_no_party_of_its_own(void)
{
    check(xmip.authorize(allowed, NULL) == XMIP_OK, "authorize is OK");
    XmipEventFilter filter = {0};
    filter.scope = xmip_str("xmip:///c-proven");

    XmipEventSubscription *out = NULL;
    uint8_t said[512];
    size_t said_len = 0;
    XmipStatus status = xmip.subscribe(xmip_str(PROGRAM), xmip_str(directory), &filter, 0,
                                       &out, said, sizeof said, &said_len);
    check(status == XMIP_E_AUTH && out == NULL,
          "subscribe where no node serves is XMIP_E_AUTH, whatever the policy allows");
    check(says(said, said_len, NO_PARTY), "the sentence says no Party holds the account");
    printf("        said: %.*s\n", (int)said_len, (const char *)said);

    said_len = 0;
    status = xmip.listen(xmip_str(PROGRAM), xmip_str(directory), &filter, 0, told, NULL, &out,
                         said, sizeof said, &said_len);
    check(status == XMIP_E_AUTH && out == NULL, "listen is refused alike");
    check(says(said, said_len, NO_PARTY), "with the same sentence");
}

static void an_event_published_here_crosses_whole(void)
{
    const XmipStr diagnostics[] = {xmip_str("status"), xmip_str("refused")};
    XmipEvent event = {0};
    event.type = xmip_str("se.xmip.receive.failure");
    event.action = XMIP_ACTION_RECEIVE;
    event.outcome = XMIP_OUTCOME_FAILURE;
    event.scope = xmip_str("xmip:///c-proven/node/n1/receive/orders");
    event.artifact = xmip_str("orders");
    event.diagnostics = diagnostics;
    event.diagnostics_len = 2;
    size_t delivered = 1;
    check(xmip.publish(&event, &delivered) == XMIP_OK && delivered == 0,
          "an Event is published, and no subscription here takes it");

    XmipEvent malformed = event;
    malformed.journey = xmip_str("not a uuid");
    check(xmip.publish(&malformed, &delivered) == XMIP_E_MALFORMED,
          "an identifier that is not a UUID is XMIP_E_MALFORMED");
    XmipEvent odd = event;
    odd.diagnostics_len = 1;
    check(xmip.publish(&odd, &delivered) == XMIP_E_INVALID,
          "an odd count of diagnostics is XMIP_E_INVALID");
}

static void nobody_unheard_is_said_so(void)
{
    uint8_t listed[256];
    size_t listed_len = 0;
    check(xmip.unheard(listed, sizeof listed, &listed_len) == XMIP_OK
              && listed_len == strlen("{\"unheard\":[]}")
              && memcmp(listed, "{\"unheard\":[]}", listed_len) == 0,
          "nobody unheard is said so");
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
    a_program_subscribing_names_no_party_of_its_own();
    an_event_published_here_crosses_whole();
    nobody_unheard_is_said_so();
    xmip_event_library_close(&xmip);
    if (failures) {
        printf("FAILED. %d check(s) of the C binding\n", failures);
        return 1;
    }
    printf("OK. C binding\n");
    return 0;
}
