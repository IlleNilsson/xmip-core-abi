// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.io.IOException;
import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.TreeMap;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * The Java binding through the runtime's real library: the header's numbers
 * and names, a subscription that drains what it matches, a listener called
 * back through an upcall, and an Event that reaches its subscriber within a
 * millisecond of its publish.
 *
 * <pre>java --enable-preview -cp build/java se.xmip.event.EventBindingTest
 *     &lt;runtime library&gt; &lt;audit directory&gt; &lt;include directory&gt;</pre>
 *
 * Exit 0 when every check is OK.
 */
public final class EventBindingTest {
    private static final String SUBSCRIBER = "0198a3c4-0000-7000-8000-000000000042";
    private static final String PROGRAM = "xmip-core-abi java test";
    private static final int WARMUP = 300;
    private static final int MEASURED = 500;

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
            aSubscriberIsAParty(xmip, directory);
            aSubscriptionDrainsWhatItMatches(xmip, directory);
            aDrainedEventArrivesWithinAMillisecond(xmip, directory);
            aListenerIsCalledBackWithinAMillisecond(xmip, directory);
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
        check(!entrypoints.isEmpty() && entrypoints.equals(bound),
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

    private static void aSubscriberIsAParty(Library xmip, String directory) {
        try {
            xmip.subscribe(PROGRAM, directory, "", Filter.any(), 0).close();
            check(false, "no subscriber is refused");
        } catch (EventException refused) {
            check(refused.status() == -1, "no subscriber is XMIP_E_INVALID");
        }
    }

    private static Event raised(String scope, Outcome outcome, Map<String, String> said) {
        return new Event("", "se.xmip.receive.failure", 0, Action.RECEIVE, outcome, scope,
                "", "", "", "", "", "orders", "", said);
    }

    private static void aSubscriptionDrainsWhatItMatches(Library xmip, String directory) {
        Filter failures = new Filter(List.of(), List.of(Outcome.FAILURE), "xmip:///java-drain", "");
        try (Subscription subscription = xmip.subscribe(PROGRAM, directory, SUBSCRIBER,
                failures, 0)) {
            String scope = "xmip:///java-drain/node/n1/receive/orders";
            check(xmip.publish(raised(scope, Outcome.FAILURE, Map.of("status", "refused"))) == 1,
                    "a matching Event is delivered to one subscription");
            check(xmip.publish(raised(scope, Outcome.SUCCESS, Map.of())) == 0,
                    "an Event of another outcome is delivered to none");
            Optional<Delivery> delivery = subscription.next(Duration.ofSeconds(1), 16);
            check(delivery.isPresent() && delivery.get().events().size() == 1,
                    "next hands over the one Event");
            delivery.ifPresent(d -> {
                Event event = d.events().get(0);
                check(event.type().equals("se.xmip.receive.failure"), "its type");
                check(event.scope().equals(scope), "its scope");
                check(event.artifact().equals("orders"), "its artifact");
                check(event.id().length() == 36, "its id was minted as a UUID");
                check(event.outcome() == Outcome.FAILURE, "its outcome");
                check("refused".equals(event.diagnostics().get("status")), "its diagnostics");
            });
            check(subscription.next(Duration.ofMillis(1), 16).isEmpty(),
                    "an empty queue times out");
        }
    }

    /** Arrivals: when each one was seen, and how many have been. */
    private static final class Timing {
        final AtomicInteger received = new AtomicInteger();
        final long[] at = new long[WARMUP + MEASURED];

        void arrived(long when) {
            int slot = received.get();
            if (slot < at.length) {
                at[slot] = when;
            }
            received.incrementAndGet();
        }

        boolean awaited(int i, long since) {
            while (received.get() <= i) {
                if (System.nanoTime() - since > 2_000_000_000L) {
                    return false;
                }
                Thread.onSpinWait();
            }
            return true;
        }
    }

    /** Median, p99 and worst in microseconds, printed; the first two returned. */
    private static double[] spread(String what, long[] from, long[] to) {
        double[] micros = new double[MEASURED];
        for (int i = 0; i < MEASURED; i++) {
            micros[i] = (to[WARMUP + i] - from[WARMUP + i]) / 1000.0;
        }
        Arrays.sort(micros);
        double median = micros[MEASURED / 2];
        double p99 = micros[MEASURED * 99 / 100];
        System.out.printf(Locale.ROOT,
                "        %-26s median %8.1f us, p99 %8.1f us, worst %8.1f us over %d%n",
                what, median, p99, micros[MEASURED - 1], MEASURED);
        return new double[] {median, p99};
    }

    /**
     * Publish one Event and wait for it, then wake a plain thread on a plain
     * blocking queue and wait for that, round after round. The rule is a
     * millisecond apart from load, so each bound is 1 ms plus the plain wake's.
     */
    private static void measure(Library xmip, Timing timing, String scope, String path)
            throws InterruptedException {
        long[] sent = new long[WARMUP + MEASURED];
        long[] woken = new long[WARMUP + MEASURED];
        Timing plain = new Timing();
        BlockingQueue<Long> queue = new LinkedBlockingQueue<>();
        Thread waiter = new Thread(() -> {
            try {
                for (int seen = 0; seen < WARMUP + MEASURED; seen++) {
                    queue.take();
                    plain.arrived(System.nanoTime());
                }
            } catch (InterruptedException stopped) {
                Thread.currentThread().interrupt();
            }
        });
        waiter.start();
        Event event = raised(scope, Outcome.FAILURE, Map.of());
        boolean ok = true;
        for (int i = 0; ok && i < sent.length; i++) {
            sent[i] = System.nanoTime();
            ok = xmip.publish(event) == 1 && timing.awaited(i, sent[i]);
            woken[i] = System.nanoTime();
            queue.add(woken[i]);
            ok = ok && plain.awaited(i, woken[i]);
        }
        waiter.interrupt();
        waiter.join();
        if (!ok) {
            check(false, path + ": every Event of the latency run arrived");
            return;
        }
        double[] event99 = spread("publish to " + path, sent, timing.at);
        double[] plain99 = spread("a plain wake beside it", woken, plain.at);
        check(event99[0] < 1000.0 + plain99[0], path + ": median under 1 ms plus the plain's");
        check(event99[1] < 1000.0 + plain99[1], path + ": p99 under 1 ms plus the plain's");
    }

    private static void aDrainedEventArrivesWithinAMillisecond(Library xmip, String directory)
            throws InterruptedException {
        Timing timing = new Timing();
        AtomicBoolean stop = new AtomicBoolean();
        try (Subscription subscription = xmip.subscribe(PROGRAM, directory, SUBSCRIBER,
                Filter.at("xmip:///java-drain-latency"), 0)) {
            Thread receiver = new Thread(() -> {
                while (!stop.get()) {
                    subscription.next(Duration.ofMillis(100), 16).ifPresent(d -> {
                        long when = System.nanoTime();
                        d.events().forEach(e -> timing.arrived(when));
                    });
                }
            });
            receiver.start();
            measure(xmip, timing, "xmip:///java-drain-latency/node/n1", "next");
            stop.set(true);
            receiver.join();
        }
    }

    private static void aListenerIsCalledBackWithinAMillisecond(Library xmip, String directory)
            throws InterruptedException {
        Timing timing = new Timing();
        try (Subscription listening = xmip.listen(PROGRAM, directory, SUBSCRIBER,
                Filter.at("xmip:///java-listen"), 0, event -> timing.arrived(System.nanoTime()))) {
            try {
                listening.next(Duration.ofMillis(1), 1);
                check(false, "a listening subscription is refused a drain");
            } catch (EventException refused) {
                check(refused.status() == -3, "a listening subscription is never drained");
            }
            measure(xmip, timing, "xmip:///java-listen/node/n1", "callback");
        }
    }
}
