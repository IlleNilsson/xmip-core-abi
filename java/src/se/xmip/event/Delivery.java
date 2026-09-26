// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.util.List;

/** One drain: the Events handed over, and how many a full queue refused since the last. */
public record Delivery(List<Event> events, long refused) {
}
