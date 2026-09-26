// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import static java.lang.foreign.MemoryLayout.PathElement.groupElement;
import static java.lang.foreign.ValueLayout.ADDRESS;
import static java.lang.foreign.ValueLayout.JAVA_BYTE;
import static java.lang.foreign.ValueLayout.JAVA_INT;
import static java.lang.foreign.ValueLayout.JAVA_LONG;

import java.lang.foreign.Arena;
import java.lang.foreign.MemoryLayout;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.StructLayout;
import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * The header's structs as the foreign-function API lays them out, and the
 * copy of each across the boundary. {@code size_t} is a 64-bit integer: the
 * binding supports 64-bit platforms, which are the runtime's.
 */
final class Layout {
    /** {@code XmipStr}: a pointer and a length, never null-terminated. */
    static final StructLayout STR = MemoryLayout.structLayout(
            ADDRESS.withName("ptr"), JAVA_LONG.withName("len"));

    /** {@code XmipEvent}. */
    static final StructLayout EVENT = MemoryLayout.structLayout(
            STR.withName("id"), STR.withName("type"),
            JAVA_LONG.withName("time_unix_nanos"),
            JAVA_INT.withName("action"), JAVA_INT.withName("outcome"),
            STR.withName("scope"), STR.withName("journey"), STR.withName("message"),
            STR.withName("stream"), STR.withName("endpoint"), STR.withName("module"),
            STR.withName("artifact"), STR.withName("party"),
            ADDRESS.withName("diagnostics"), JAVA_LONG.withName("diagnostics_len"));

    /** {@code XmipEventFilter}. */
    static final StructLayout FILTER = MemoryLayout.structLayout(
            ADDRESS.withName("types"), JAVA_LONG.withName("types_len"),
            ADDRESS.withName("outcomes"), JAVA_LONG.withName("outcomes_len"),
            STR.withName("scope"), STR.withName("party"));

    private static final String[] TEXTS = {
        "journey", "message", "stream", "endpoint", "module", "artifact", "party"
    };

    // The Event's offsets, looked up once: an Event is copied on every delivery.
    private static final long ID = at(EVENT, "id");
    private static final long TYPE = at(EVENT, "type");
    private static final long TIME = at(EVENT, "time_unix_nanos");
    private static final long ACTION = at(EVENT, "action");
    private static final long OUTCOME = at(EVENT, "outcome");
    private static final long SCOPE = at(EVENT, "scope");
    private static final long DIAGNOSTICS = at(EVENT, "diagnostics");
    private static final long DIAGNOSTICS_LEN = at(EVENT, "diagnostics_len");
    private static final long[] TEXT_AT = new long[TEXTS.length];

    static {
        for (int i = 0; i < TEXTS.length; i++) {
            TEXT_AT[i] = at(EVENT, TEXTS[i]);
        }
    }

    private Layout() {
    }

    private static long at(StructLayout layout, String name) {
        return layout.byteOffset(groupElement(name));
    }

    /** The string an {@code XmipStr} at {@code offset} of {@code segment} borrows. */
    static String text(MemorySegment segment, long offset) {
        long len = segment.get(JAVA_LONG, offset + 8);
        if (len == 0) {
            return "";
        }
        MemorySegment bytes = segment.get(ADDRESS, offset).reinterpret(len);
        byte[] copy = new byte[Math.toIntExact(len)];
        MemorySegment.copy(bytes, JAVA_BYTE, 0, copy, 0, copy.length);
        return new String(copy, StandardCharsets.UTF_8);
    }

    /** Write {@code value} as an {@code XmipStr} at {@code offset}, its bytes in {@code arena}. */
    static void text(Arena arena, MemorySegment segment, long offset, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        MemorySegment copy = MemorySegment.NULL;
        if (bytes.length > 0) {
            copy = arena.allocate(bytes.length, 1);
            MemorySegment.copy(bytes, 0, copy, JAVA_BYTE, 0, bytes.length);
        }
        segment.set(ADDRESS, offset, copy);
        segment.set(JAVA_LONG, offset + 8, bytes.length);
    }

    /** An {@code XmipStr} by value, for a downcall's argument. */
    static MemorySegment str(Arena arena, String value) {
        MemorySegment str = arena.allocate(STR);
        text(arena, str, 0, value);
        return str;
    }

    /** The Event at {@code address}, copied out. */
    static Event event(MemorySegment address) {
        MemorySegment event = address.reinterpret(EVENT.byteSize());
        long count = event.get(JAVA_LONG, DIAGNOSTICS_LEN);
        Map<String, String> diagnostics = new LinkedHashMap<>();
        if (count > 0) {
            MemorySegment pairs = event.get(ADDRESS, DIAGNOSTICS)
                    .reinterpret(count * STR.byteSize());
            for (long i = 0; i + 1 < count; i += 2) {
                diagnostics.put(text(pairs, i * STR.byteSize()),
                        text(pairs, (i + 1) * STR.byteSize()));
            }
        }
        String[] texts = new String[TEXTS.length];
        for (int i = 0; i < TEXTS.length; i++) {
            texts[i] = text(event, TEXT_AT[i]);
        }
        return new Event(
                text(event, ID),
                text(event, TYPE),
                event.get(JAVA_LONG, TIME),
                Action.of(event.get(JAVA_INT, ACTION)),
                Outcome.of(event.get(JAVA_INT, OUTCOME)),
                text(event, SCOPE),
                texts[0], texts[1], texts[2], texts[3], texts[4], texts[5], texts[6],
                Collections.unmodifiableMap(diagnostics));
    }

    /** {@code event} as an {@code XmipEvent} in {@code arena}. */
    static MemorySegment event(Arena arena, Event event) {
        MemorySegment wire = arena.allocate(EVENT);
        text(arena, wire, ID, event.id());
        text(arena, wire, TYPE, event.type());
        wire.set(JAVA_LONG, TIME, event.timeUnixNanos());
        wire.set(JAVA_INT, ACTION, event.action().code());
        wire.set(JAVA_INT, OUTCOME, event.outcome().code());
        text(arena, wire, SCOPE, event.scope());
        String[] texts = {
            event.journey(), event.message(), event.stream(), event.endpoint(),
            event.module(), event.artifact(), event.party()
        };
        for (int i = 0; i < TEXTS.length; i++) {
            text(arena, wire, TEXT_AT[i], texts[i]);
        }
        Map<String, String> diagnostics = event.diagnostics();
        MemorySegment pairs = MemorySegment.NULL;
        if (!diagnostics.isEmpty()) {
            pairs = arena.allocate(STR.byteSize() * 2 * diagnostics.size(), 8);
            long offset = 0;
            for (Map.Entry<String, String> pair : diagnostics.entrySet()) {
                text(arena, pairs, offset, pair.getKey());
                text(arena, pairs, offset + STR.byteSize(), pair.getValue());
                offset += 2 * STR.byteSize();
            }
        }
        wire.set(ADDRESS, DIAGNOSTICS, pairs);
        wire.set(JAVA_LONG, DIAGNOSTICS_LEN, 2L * diagnostics.size());
        return wire;
    }

    /** {@code filter} as an {@code XmipEventFilter} in {@code arena}. */
    static MemorySegment filter(Arena arena, Filter filter) {
        MemorySegment wire = arena.allocate(FILTER);
        List<String> types = filter.types();
        MemorySegment list = MemorySegment.NULL;
        if (!types.isEmpty()) {
            list = arena.allocate(STR.byteSize() * types.size(), 8);
            for (int i = 0; i < types.size(); i++) {
                text(arena, list, i * STR.byteSize(), types.get(i));
            }
        }
        wire.set(ADDRESS, at(FILTER, "types"), list);
        wire.set(JAVA_LONG, at(FILTER, "types_len"), types.size());
        List<Outcome> outcomes = filter.outcomes();
        MemorySegment codes = MemorySegment.NULL;
        if (!outcomes.isEmpty()) {
            codes = arena.allocate(JAVA_INT.byteSize() * outcomes.size(), 4);
            for (int i = 0; i < outcomes.size(); i++) {
                codes.set(JAVA_INT, i * JAVA_INT.byteSize(), outcomes.get(i).code());
            }
        }
        wire.set(ADDRESS, at(FILTER, "outcomes"), codes);
        wire.set(JAVA_LONG, at(FILTER, "outcomes_len"), outcomes.size());
        text(arena, wire, at(FILTER, "scope"), filter.scope());
        text(arena, wire, at(FILTER, "party"), filter.party());
        return wire;
    }
}
