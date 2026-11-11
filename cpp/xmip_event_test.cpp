// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

// xmip_event_test.cpp - include/xmip_event.hpp through the runtime's real
// library: a program subscribing names no Party of its own. It is the
// account the operating system says it runs as, resolved through the
// Parties of the node serving in its process (ADR-0019, amendment
// 2026-11-11); this test's process serves no node, so subscribe and listen
// throw XMIP_E_AUTH with the sentence, and publish, the policy and who is
// unheard still cross.
//
//     xmip_event_test <runtime library> <audit directory>
//
// Both default to XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY; the
// audit directory is never empty here. Exit 0 when every check is OK.

#include <cstdio>
#include <cstdlib>
#include <string>

#include "xmip_event.hpp"

namespace {

using namespace xmip::event;

constexpr std::string_view program = "xmip-core-abi cpp test";
constexpr std::string_view no_party = "is no Party of a node serving in this process";

int failures = 0;

void check(bool ok, const std::string &what) {
    std::printf("%s  %s\n", ok ? "OK    " : "FAILED", what.c_str());
    failures += ok ? 0 : 1;
}

Draft raised(std::string_view scope) {
    Draft draft;
    draft.type = "se.xmip.receive.failure";
    draft.action = XMIP_ACTION_RECEIVE;
    draft.outcome = XMIP_OUTCOME_FAILURE;
    draft.scope = scope;
    draft.artifact = "orders";
    draft.diagnostics = {{"status", "refused"}};
    return draft;
}

void a_program_subscribing_names_no_party_of_its_own(Library &xmip,
                                                     const std::string &directory) {
    // Whatever the policy allows, a subscriber must prove to be a Party.
    xmip.authorize_by([](auto &&...) { return XMIP_EVENT_ALLOW; });
    Filter filter;
    filter.scope = "xmip:///cpp-proven";
    try {
        xmip.subscribe(program, directory, filter);
        check(false, "subscribe where no node serves is refused");
    } catch (const Error &refused) {
        check(refused.status() == XMIP_E_AUTH, "subscribe where no node serves is XMIP_E_AUTH");
        check(std::string(refused.what()).find(no_party) != std::string::npos,
              "the sentence says no Party holds the account");
        std::printf("        said: %s\n", refused.what());
    }
    try {
        xmip.listen(program, directory, filter, [](const EventView &) {});
        check(false, "listen where no node serves is refused");
    } catch (const Error &refused) {
        check(refused.status() == XMIP_E_AUTH, "listen is refused alike");
    }
}

void an_event_published_here_crosses_whole(const Library &xmip) {
    check(xmip.publish(raised("xmip:///cpp-proven/node/n1/receive/orders")) == 0,
          "an Event is published, and no subscription here takes it");
    Draft malformed = raised("xmip:///cpp-proven/node/n1");
    malformed.journey = "not a uuid";
    try {
        xmip.publish(malformed);
        check(false, "an identifier that is not a UUID is refused");
    } catch (const Error &refused) {
        check(refused.status() == XMIP_E_MALFORMED,
              "an identifier that is not a UUID is XMIP_E_MALFORMED");
    }
    check(xmip.unheard() == "{\"unheard\":[]}", "nobody unheard is said so");
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
        a_program_subscribing_names_no_party_of_its_own(xmip, directory);
        an_event_published_here_crosses_whole(xmip);
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
