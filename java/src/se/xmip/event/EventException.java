// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright the Xmip authors.

package se.xmip.event;

/** A status other than {@code XMIP_OK}, with what the runtime said about it. */
public final class EventException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    private final int status;

    EventException(int status, String said) {
        super(said.isEmpty() ? "Xmip status " + status : said);
        this.status = status;
    }

    /** The {@code XmipStatus}, as xmip_module.h section 3 numbers it. */
    public int status() {
        return status;
    }
}
