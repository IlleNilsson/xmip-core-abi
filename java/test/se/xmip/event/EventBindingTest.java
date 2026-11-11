// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.io.IOException;
import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Map;
import java.util.TreeMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * The Java binding through the runtime's real library: the header's numbers
 * and names, and a program subscribing that names no Party of its own. It is
 * the account the operating system says it runs as, resolved through the
 * Parties of the node serving in its process (ADR-0019, amendment
 * 2026-11-11); this test's process serves no node, so subscribe and listen
 * are refused XMIP_E_AUTH with the sentence, and publish, the policy and who
 * is unheard still cross.
 *
 * <pre>java --enable-native-access=ALL-UNNAMED -cp build/java se.xmip.event.EventBindingTest
 *     &lt;runtime library&gt; &lt;audit directory&gt; &lt;include directory&gt;</pre>
 *
 * Exit 0 when every check is OK.
 */
public final class EventBindingTest {
    private static final String PROGRAM = "xmip-core-abi java test";
    private static final String NO_PARTY = "is no Party of a node serving in this process";

    private static int failures;

    private EventBindingTest() {
    }

    private static void check(boolean ok, String what) {
        System.out.println((ok ? "OK      " : "FAILED  ") + what);
        if (!ok) {
            failures++;
        }
    }

    public static void main(String[] args) throws Exception {
        if (args.length < 3) {
            System.err.println("usage: EventBindingTest <runtime library> <audit directory>"
                    + " <include directory>");
            System.exit(2);
        }
        String directory = args[1];
        theHeaderNumbersAreTheBindings(Path.of(args[2]));
        try (Library xmip = Library.load(Path.of(args[0]))) {
            aProgramSubscribingNamesNoPartyOfItsOwn(xmip, directory);
            anEventPublishedHereCrossesWhole(xmip);
        }
        if (failures > 0) {
            System.out.println("FAILED. " + failures + " check(s) of the Java binding");
            System.exit(1);
        }
        System.out.println("OK. Java binding");
    }

    private static Map<String, Integer> numbers(String header, String prefix) {
        Map<String, Integer> found = new TreeMap<>();
        Matcher m = Pattern.compile(prefix + "(\\w+)\\s*=\\s*(\\d+)").matcher(header);
        while (m.find()) {
            found.put(m.group(1), Integer.parseInt(m.group(2)));
        }
        return found;
    }

    private static void theHeaderNumbersAreTheBindings(Path include) throws IOException {
        String operate = Files.readString(include.resolve("xmip_operate.h"));
        String module = Files.readString(include.resolve("xmip_module.h"));
        Map<String, Integer> outcomes = new TreeMap<>();
        for (Outcome outcome : Outcome.values()) {
            outcomes.put(outcome.name(), outcome.code());
        }
        check(outcomes.equals(numbers(operate, "XMIP_OUTCOME_")), "XmipOutcome is the header's");
        Map<String, Integer> actions = new TreeMap<>();
        for (Action action : Action.values()) {
            actions.put(action.name(), action.code());
        }
        check(actions.equals(numbers(operate, "XMIP_ACTION_")), "XmipAction is the header's");

        Map<String, String> entrypoints = new TreeMap<>();
        Matcher m = Pattern.compile("#define XMIP_EVENT_(\\w+)_ENTRYPOINT\\s+\"(\\w+)\"")
                .matcher(operate);
        while (m.find()) {
            entrypoints.put(m.group(1) + "_ENTRYPOINT", m.group(2));
        }
        Map<String, String> bound = new TreeMap<>();
        for (Field field : Library.class.getFields()) {
            if (field.getName().endsWith("_ENTRYPOINT")) {
                bound.put(field.getName(), (String) get(field));
            }
        }
        // What an operator lists and does of Event subscriptions is section
        // 11's too and bound by the operator surfaces, not by a program's
        // binding: every name this binds is the header's.
        check(!bound.isEmpty() && entrypoints.entrySet().containsAll(bound.entrySet()),
                "the entrypoint names are the header's");
        check(module.matches("(?s).*#define XMIP_OK\\s+" + Library.OK + "\\b.*")
                && module.matches("(?s).*#define XMIP_E_TIMEOUT\\s+\\(" + Library.E_TIMEOUT
                        + "\\).*"), "XMIP_OK and XMIP_E_TIMEOUT are the header's");
    }

    private static Object get(Field field) {
        try {
            return field.get(null);
        } catch (IllegalAccessException failed) {
            throw new IllegalStateException(failed);
        }
    }

    private static void aProgramSubscribingNamesNoPartyOfItsOwn(Library xmip, String directory) {
        // Whatever the policy allows, a subscriber must prove to be a Party.
        xmip.authorizeBy(asked -> Boolean.TRUE);
        Filter here = Filter.at("xmip:///java-proven");
        try {
            xmip.subscribe(PROGRAM, directory, here, 0).close();
            check(false, "subscribe where no node serves is refused");
        } catch (EventException refused) {
            check(refused.status() == -23, "subscribe where no node serves is XMIP_E_AUTH");
            check(refused.getMessage().contains(NO_PARTY),
                    "the sentence says no Party holds the account");
            System.out.println("        said: " + refused.getMessage());
        }
        try {
            xmip.listen(PROGRAM, directory, here, 0, event -> { }).close();
            check(false, "listen where no node serves is refused");
        } catch (EventException refused) {
            check(refused.status() == -23, "listen is refused alike");
        }
    }

    private static void anEventPublishedHereCrossesWhole(Library xmip) {
        String scope = "xmip:///java-proven/node/n1/receive/orders";
        Event raised = new Event("", "se.xmip.receive.failure", 0, Action.RECEIVE,
                Outcome.FAILURE, scope, "", "", "", "", "", "orders", "",
                Map.of("status", "refused"));
        check(xmip.publish(raised) == 0,
                "an Event is published, and no subscription here takes it");
        Event malformed = new Event("", "se.xmip.receive.failure", 0, Action.RECEIVE,
                Outcome.FAILURE, scope, "not a uuid", "", "", "", "", "orders", "", Map.of());
        try {
            xmip.publish(malformed);
            check(false, "an identifier that is not a UUID is refused");
        } catch (EventException refused) {
            check(refused.status() == -10, "an identifier that is not a UUID is XMIP_E_MALFORMED");
        }
        check(xmip.unheard().equals("{\"unheard\":[]}"), "nobody unheard is said so");
    }
}