// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

import java.util.List;

/**
 * One drain: the Events handed over, how many a full queue refused since the
 * last, and who is not heard now, as the header writes it -
 * {@code {"changed":true,"unheard":[{"by","node","since_unix_nanos","why","said"}]}}:
 * a member of the cluster whose Events are not among any delivered until it is
 * heard again, {@code said} the one line every surface shows. A drain wakes
 * for a change of those alone, with no Event, so nothing missing is silent.
 */
public record Delivery(List<Event> events, long refused, String unheard) {
}
