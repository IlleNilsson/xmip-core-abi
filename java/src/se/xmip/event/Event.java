// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.util.Map;

/**
 * {@code XmipEvent}: references, never a payload. Copied out of the batch or
 * callback it arrived in. Identifiers are UUIDs in 8-4-4-4-12 form, and every
 * optional string is empty where there is none. Diagnostics are name to
 * value, in the order the runtime gave them.
 */
public record Event(
        String id,
        String type,
        long timeUnixNanos,
        Action action,
        Outcome outcome,
        String scope,
        String journey,
        String message,
        String stream,
        String endpoint,
        String module,
        String artifact,
        String party,
        Map<String, String> diagnostics) {

    /**
     * An Event to publish with nothing but what the runtime requires: an
     * empty id is minted and a time of 0 is now.
     */
    public static Event raised(String type, Action action, Outcome outcome, String scope) {
        return new Event("", type, 0, action, outcome, scope, "", "", "", "", "", "", "",
                Map.of());
    }
}
