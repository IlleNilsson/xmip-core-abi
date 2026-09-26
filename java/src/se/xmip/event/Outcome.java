// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

/** {@code XmipOutcome}, xmip_operate.h section 11: how an action ended. */
public enum Outcome {
    SUCCESS(0),
    FAILURE(1),
    REJECTION(2),
    WAITING(3),
    PAUSE(4),
    TIMEOUT(5),
    EXHAUSTED_RETRIES(6),
    DISMISSAL(7);

    private final int code;

    Outcome(int code) {
        this.code = code;
    }

    /** The header's number for this outcome. */
    public int code() {
        return code;
    }

    /** The outcome the header numbers {@code code}. */
    public static Outcome of(int code) {
        for (Outcome outcome : values()) {
            if (outcome.code == code) {
                return outcome;
            }
        }
        throw new IllegalArgumentException("no XmipOutcome " + code);
    }
}
