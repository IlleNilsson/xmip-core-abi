// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

/** {@code XmipAction}, xmip_operate.h section 11: the stage whose action completed. */
public enum Action {
    RECEIVE(0),
    PROCESS(1),
    SEND(2);

    private final int code;

    Action(int code) {
        this.code = code;
    }

    /** The header's number for this action. */
    public int code() {
        return code;
    }

    /** The action the header numbers {@code code}. */
    public static Action of(int code) {
        for (Action action : values()) {
            if (action.code == code) {
                return action;
            }
        }
        throw new IllegalArgumentException("no XmipAction " + code);
    }
}
