// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import static java.lang.foreign.ValueLayout.ADDRESS;
import static java.lang.foreign.ValueLayout.JAVA_BYTE;
import static java.lang.foreign.ValueLayout.JAVA_INT;
import static java.lang.foreign.ValueLayout.JAVA_LONG;

import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.invoke.MethodHandle;
import java.lang.invoke.MethodHandles;
import java.lang.invoke.MethodType;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.function.Consumer;
import java.util.function.Function;

/**
 * The runtime's library, loaded by path, and xmip_operate.h section 11's six
 * symbols looked up once by the names the header gives them. Thin: which
 * Events a filter matches, whether a subscriber may see them, the queue and
 * the audit are the runtime's (ADR-0065). Outlives every EventSubscription.
 */
public final class Library implements AutoCloseable {
    public static final String SUBSCRIBE_ENTRYPOINT = "xmip_event_subscribe_v1";
    public static final String NEXT_ENTRYPOINT = "xmip_event_next_v1";
    public static final String BATCH_FREE_ENTRYPOINT = "xmip_event_batch_free_v1";
    public static final String LISTEN_ENTRYPOINT = "xmip_event_listen_v1";
    public static final String UNSUBSCRIBE_ENTRYPOINT = "xmip_event_unsubscribe_v1";
    public static final String PUBLISH_ENTRYPOINT = "xmip_event_publish_v1";
    public static final String AUTHORIZE_ENTRYPOINT = "xmip_event_authorize_v1";
    public static final String BATCH_UNHEARD_ENTRYPOINT = "xmip_event_batch_unheard_v1";
    public static final String UNHEARD_ENTRYPOINT = "xmip_event_unheard_v1";

    /** {@code XMIP_OK} and {@code XMIP_E_TIMEOUT}, xmip_module.h section 3. */
    static final int OK = 0;
    static final int E_TIMEOUT = -21;

    private static final FunctionDescriptor CALLBACK =
            FunctionDescriptor.ofVoid(ADDRESS, ADDRESS);
    private static final FunctionDescriptor DECIDE = FunctionDescriptor.of(JAVA_INT, ADDRESS,
            Layout.STR, Layout.STR, Layout.STR, Layout.STR, Layout.STR);

    private final Arena loaded;
    private final Linker linker = Linker.nativeLinker();
    private final MethodHandle subscribe;
    private final MethodHandle next;
    private final MethodHandle batchFree;
    private final MethodHandle listen;
    private final MethodHandle unsubscribe;
    private final MethodHandle publish;
    private final MethodHandle authorize;
    /** The policy's upcall, alive until it is replaced. */
    private Arena policy;
    private final MethodHandle batchUnheard;
    private final MethodHandle unheard;

    private Library(Path path) {
        loaded = Arena.ofShared();
        SymbolLookup lookup = SymbolLookup.libraryLookup(path, loaded);
        subscribe = bind(lookup, SUBSCRIBE_ENTRYPOINT, FunctionDescriptor.of(JAVA_INT,
                Layout.STR, Layout.STR, Layout.STR, ADDRESS, JAVA_LONG, ADDRESS,
                ADDRESS, JAVA_LONG, ADDRESS));
        next = bind(lookup, NEXT_ENTRYPOINT, FunctionDescriptor.of(JAVA_INT,
                ADDRESS, JAVA_INT, JAVA_LONG, ADDRESS, ADDRESS, ADDRESS, ADDRESS));
        batchFree = bind(lookup, BATCH_FREE_ENTRYPOINT, FunctionDescriptor.ofVoid(ADDRESS));
        listen = bind(lookup, LISTEN_ENTRYPOINT, FunctionDescriptor.of(JAVA_INT,
                Layout.STR, Layout.STR, Layout.STR, ADDRESS, JAVA_LONG, ADDRESS, ADDRESS,
                ADDRESS, ADDRESS, JAVA_LONG, ADDRESS));
        unsubscribe = bind(lookup, UNSUBSCRIBE_ENTRYPOINT, FunctionDescriptor.ofVoid(ADDRESS));
        publish = bind(lookup, PUBLISH_ENTRYPOINT,
                FunctionDescriptor.of(JAVA_INT, ADDRESS, ADDRESS));
        authorize = bind(lookup, AUTHORIZE_ENTRYPOINT,
                FunctionDescriptor.of(JAVA_INT, ADDRESS, ADDRESS));
        batchUnheard = bind(lookup, BATCH_UNHEARD_ENTRYPOINT,
                FunctionDescriptor.of(JAVA_INT, ADDRESS, ADDRESS, JAVA_LONG, ADDRESS));
        unheard = bind(lookup, UNHEARD_ENTRYPOINT,
                FunctionDescriptor.of(JAVA_INT, ADDRESS, JAVA_LONG, ADDRESS));
    }

    /** Load the runtime's library at {@code path}. */
    public static Library load(Path path) {
        return new Library(path);
    }

    private MethodHandle bind(SymbolLookup lookup, String name, FunctionDescriptor shape) {
        MemorySegment symbol = lookup.find(name).orElseThrow(
                () -> new EventException(-4, "the library has no " + name));
        return linker.downcallHandle(symbol, shape);
    }

    /**
     * Subscribe to drain. {@code program} and {@code directory} say where the
     * subscription is audited (an empty directory: the capability decides);
     * {@code subscriber} is the Party's UUID. Throws {@link EventException},
     * with the authorization gate's sentence when refused.
     */
    public EventSubscription subscribe(String program, String directory, String subscriber,
            Filter filter, long capacity) {
        return open(program, directory, subscriber, filter, capacity, null);
    }

    /**
     * Subscribe to be called back with each Event, on a thread the runtime
     * starts for this subscription, one call at a time. An exception the
     * callback throws is dropped, since nothing may unwind into the runtime.
     */
    public EventSubscription listen(String program, String directory, String subscriber,
            Filter filter, long capacity, Consumer<Event> callback) {
        return open(program, directory, subscriber, filter, capacity, callback);
    }

    /** One attempt to subscribe, as the program's policy is asked of it. */
    public record Asked(String party, String mechanism, String value, String scope,
            String type) {
    }

    /**
     * Hand this process's hub {@code decide} as its policy of who may
     * subscribe: it answers {@code TRUE} (allow), {@code FALSE} (deny) or
     * {@code null} (no opinion), and nothing having an opinion is a refusal.
     * It replaces the policy handed before; {@code null} hands none. An
     * exception it throws is no opinion.
     */
    public synchronized void authorizeBy(Function<Asked, Boolean> decide) {
        Arena next = decide == null ? null : Arena.ofShared();
        try {
            MemorySegment stub = MemorySegment.NULL;
            if (decide != null) {
                MethodHandle asked = MethodHandles.lookup().findStatic(Library.class, "decided",
                        MethodType.methodType(int.class, Function.class, MemorySegment.class,
                                MemorySegment.class, MemorySegment.class, MemorySegment.class,
                                MemorySegment.class, MemorySegment.class)).bindTo(decide);
                stub = linker.upcallStub(asked, DECIDE, next);
            }
            int status = (int) authorize.invokeExact(stub, MemorySegment.NULL);
            if (status != OK) {
                throw new EventException(status, "");
            }
        } catch (EventException | Error failed) {
            closeQuietly(next);
            throw failed;
        } catch (Throwable failed) {
            closeQuietly(next);
            throw new IllegalStateException(failed);
        }
        closeQuietly(policy);
        policy = next;
    }

    /** The policy's upcall: one attempt, read, decided. */
    @SuppressWarnings("unused")
    private static int decided(Function<Asked, Boolean> decide, MemorySegment context,
            MemorySegment party, MemorySegment mechanism, MemorySegment value,
            MemorySegment scope, MemorySegment type) {
        try {
            Boolean said = decide.apply(new Asked(Layout.text(party, 0),
                    Layout.text(mechanism, 0), Layout.text(value, 0), Layout.text(scope, 0),
                    Layout.text(type, 0)));
            return said == null ? 0 : said ? 1 : -1;
        } catch (Throwable ignored) {
            return 0;
        }
    }

    /**
     * The members of the cluster this process's hub does not hear now, as
     * the header writes them: {@code {"unheard":[{"by","node",
     * "since_unix_nanos","why","said"}]}}. What a listening subscription,
     * which is never drained, asks.
     */
    public String unheard() {
        return written((out, cap, len) -> (int) unheard.invokeExact(out, cap, len));
    }

    /** Who was not heard when {@code batch} was drained, as the header writes it. */
    String batchUnheard(MemorySegment batch) {
        return written((out, cap, len) -> (int) batchUnheard.invokeExact(batch, out, cap, len));
    }

    /** A call that writes JSON into a buffer. */
    private interface Writer {
        int write(MemorySegment out, long cap, MemorySegment len) throws Throwable;
    }

    /** What {@code writer} wrote, asked again with room where it did not fit. */
    private static String written(Writer writer) {
        long size = 4096;
        while (true) {
            try (Arena arena = Arena.ofConfined()) {
                MemorySegment out = arena.allocate(size, 1);
                MemorySegment len = arena.allocate(JAVA_LONG);
                int status = writer.write(out, size, len);
                if (status != OK) {
                    throw new EventException(status, "");
                }
                long length = len.get(JAVA_LONG, 0);
                if (length <= size) {
                    byte[] text = new byte[(int) length];
                    MemorySegment.copy(out, JAVA_BYTE, 0, text, 0, text.length);
                    return new String(text, StandardCharsets.UTF_8);
                }
                size = length;
            } catch (EventException | Error failed) {
                throw failed;
            } catch (Throwable failed) {
                throw new IllegalStateException(failed);
            }
        }
    }

    /** Hand {@code event} to every matching subscription; how many queues took it. */
    public long publish(Event event) {
        try (Arena arena = Arena.ofConfined()) {
            MemorySegment delivered = arena.allocate(JAVA_LONG);
            int status = (int) publish.invokeExact(Layout.event(arena, event), delivered);
            if (status != OK) {
                throw new EventException(status, "");
            }
            return delivered.get(JAVA_LONG, 0);
        } catch (EventException | Error failed) {
            throw failed;
        } catch (Throwable failed) {
            throw new IllegalStateException(failed);
        }
    }

    /**
     * Release the library, which stays in the process (xmip_operate.h section 1).
     * Every EventSubscription must be closed first.
     */
    @Override
    public void close() {
        loaded.close();
    }

    private EventSubscription open(String program, String directory, String subscriber,
            Filter filter, long capacity, Consumer<Event> callback) {
        Arena upcall = callback == null ? null : Arena.ofShared();
        try (Arena arena = Arena.ofConfined()) {
            MemorySegment out = arena.allocate(ADDRESS);
            MemorySegment said = arena.allocate(512, 1);
            MemorySegment saidLen = arena.allocate(JAVA_LONG);
            MemorySegment wire = Layout.filter(arena, filter);
            MemorySegment p = Layout.str(arena, program);
            MemorySegment d = Layout.str(arena, directory);
            MemorySegment s = Layout.str(arena, subscriber);
            int status;
            if (callback == null) {
                status = (int) subscribe.invokeExact(p, d, s, wire, capacity, out, said,
                        said.byteSize(), saidLen);
            } else {
                MethodHandle called = MethodHandles.lookup().findStatic(Library.class,
                        "called", MethodType.methodType(void.class, Consumer.class,
                                MemorySegment.class, MemorySegment.class)).bindTo(callback);
                MemorySegment stub = linker.upcallStub(called, CALLBACK, upcall);
                status = (int) listen.invokeExact(p, d, s, wire, capacity, stub,
                        MemorySegment.NULL, out, said, said.byteSize(), saidLen);
            }
            if (status != OK) {
                byte[] text = new byte[(int) saidLen.get(JAVA_LONG, 0)];
                MemorySegment.copy(said, JAVA_BYTE, 0, text, 0, text.length);
                throw new EventException(status, new String(text, StandardCharsets.UTF_8));
            }
            return new EventSubscription(this, out.get(ADDRESS, 0), upcall);
        } catch (EventException | Error failed) {
            closeQuietly(upcall);
            throw failed;
        } catch (Throwable failed) {
            closeQuietly(upcall);
            throw new IllegalStateException(failed);
        }
    }

    private static void closeQuietly(Arena arena) {
        if (arena != null) {
            arena.close();
        }
    }

    /** The upcall: one Event, copied out, handed to the callback. */
    @SuppressWarnings("unused")
    private static void called(Consumer<Event> callback, MemorySegment context,
            MemorySegment event) {
        try {
            callback.accept(Layout.event(event));
        } catch (Throwable ignored) {
            // Nothing may unwind into the runtime's thread.
        }
    }

    int next(MemorySegment handle, int millis, long max, MemorySegment batch,
            MemorySegment events, MemorySegment len, MemorySegment refused) {
        try {
            return (int) next.invokeExact(handle, millis, max, batch, events, len, refused);
        } catch (Throwable failed) {
            throw new IllegalStateException(failed);
        }
    }

    void batchFree(MemorySegment batch) {
        try {
            batchFree.invokeExact(batch);
        } catch (Throwable failed) {
            throw new IllegalStateException(failed);
        }
    }

    void unsubscribe(MemorySegment handle) {
        try {
            unsubscribe.invokeExact(handle);
        } catch (Throwable failed) {
            throw new IllegalStateException(failed);
        }
    }
}
