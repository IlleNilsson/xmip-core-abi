// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import static java.lang.foreign.ValueLayout.ADDRESS;
import static java.lang.foreign.ValueLayout.JAVA_LONG;

import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

/**
 * A subscription the runtime holds, drained or listening. Closing it
 * unsubscribes; for a listening one, once the callback in progress returned.
 * Close it only after every thread draining it has returned from next.
 */
public final class Subscription implements AutoCloseable {
    private final Library library;
    private final Arena callback;
    private final Arena held = Arena.ofShared();
    private final Object draining = new Object();
    // next's out parameters, allocated once: next runs on every delivery.
    private final MemorySegment batch = held.allocate(ADDRESS);
    private final MemorySegment events = held.allocate(ADDRESS);
    private final MemorySegment len = held.allocate(JAVA_LONG);
    private final MemorySegment refused = held.allocate(JAVA_LONG);
    private MemorySegment handle;

    Subscription(Library library, MemorySegment handle, Arena callback) {
        this.library = library;
        this.handle = handle;
        this.callback = callback;
    }

    /**
     * Up to {@code max} Events, waiting up to {@code timeout} for the first;
     * empty when none arrived. The wait ends when an Event does, not on a
     * timer. Throws {@link EventException} on a listening subscription.
     */
    public Optional<Delivery> next(Duration timeout, int max) {
        MemorySegment live = live();
        int millis = (int) Math.min(timeout.toMillis(), 0xFFFF_FFFFL);
        synchronized (draining) {
            int status = library.next(live, millis, max, batch, events, len, refused);
            if (status == Library.E_TIMEOUT) {
                return Optional.empty();
            }
            if (status != Library.OK) {
                throw new EventException(status, "");
            }
            MemorySegment drained = batch.get(ADDRESS, 0);
            try {
                long count = len.get(JAVA_LONG, 0);
                long size = Layout.EVENT.byteSize();
                MemorySegment first = events.get(ADDRESS, 0).reinterpret(count * size);
                List<Event> copied = new ArrayList<>((int) count);
                for (long i = 0; i < count; i++) {
                    copied.add(Layout.event(first.asSlice(i * size)));
                }
                return Optional.of(new Delivery(List.copyOf(copied), refused.get(JAVA_LONG, 0)));
            } finally {
                library.batchFree(drained);
            }
        }
    }

    /** Unsubscribe; nothing is delivered afterwards. Idempotent. */
    @Override
    public synchronized void close() {
        if (handle == null) {
            return;
        }
        library.unsubscribe(handle);
        handle = null;
        held.close();
        if (callback != null) {
            callback.close();
        }
    }

    private synchronized MemorySegment live() {
        if (handle == null) {
            throw new IllegalStateException("unsubscribed");
        }
        return handle;
    }
}
