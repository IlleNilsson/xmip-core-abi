// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.util.List;

/**
 * {@code XmipEventFilter}: the Event types, the outcomes, the scope the Event
 * must have happened at or beneath, and the Party's UUID it must be about.
 * Every empty list or string is any; the runtime decides what matches.
 */
public record Filter(List<String> types, List<Outcome> outcomes, String scope, String party) {

    /** Every Event the subscriber may see. */
    public static Filter any() {
        return new Filter(List.of(), List.of(), "", "");
    }

    /** Every Event at or beneath {@code scope}. */
    public static Filter at(String scope) {
        return new Filter(List.of(), List.of(), scope, "");
    }
}
