/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_operate.h - the Xmip operator boundary, version 1.
 *
 * xmip_module.h is the boundary things plug INTO. This is the boundary that
 * drives Xmip FROM OUTSIDE - the xmip executable, the PowerShell module, the
 * GUI. ADR-0027 decides it, and its shape is opposite to the module header's:
 * a module implements a table that Xmip calls; a surface calls functions that
 * Xmip implements.
 *
 * What is shared, and only what is shared. Sections 2, 3 and 5 of the module
 * header - XmipStr, XmipStatus, the reader and writer pair - mean the same to
 * both audiences or they mean nothing to either. Everything above those is
 * separate, and this header versions apart from the module one: a surface
 * gains a command far more often than a trait gains a method, and one constant
 * for both would recompile every module for a change no module can see.
 *
 * Nothing here asks the hot path. Every call reads a snapshot the runtime
 * published; there is no call that makes execution wait for a number. ADR-0027
 * clause 6, and observability-model.md section 6 before it: the thing that
 * watches must not be able to stop the thing it watches.
 */

#ifndef XMIP_OPERATE_H
#define XMIP_OPERATE_H

#include "xmip_module.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ===================================================================== */
/* 1. Version and entrypoint                                             */
/* ===================================================================== */

#define XMIP_OPERATE_VERSION    1u
#define XMIP_OPERATE_ENTRYPOINT "xmip_operate_v1"

/* ===================================================================== */
/* 2. Scope                                                              */
/* ===================================================================== */

/*
 * Everything across this boundary that names a thing names it as an Xmip URI:
 *
 *     xmip://[userinfo@][host][:port]/path?query#fragment
 *
 * Omitted userinfo is the caller's identity; omitted host is estate-wide. The
 * path walks the one scope tree - the execution tree the Xmip Service builds
 * at startup: installation, cluster, node, host service, then a receive
 * location, an xmip process or a send location. A Party is a filter across
 * that tree, expressed in the query, never a level in it. ADR-0027 clauses 3
 * and 4.
 *
 * Borrowed, like every XmipStr. Valid for the call it was passed to.
 */
typedef XmipStr XmipScope;

/* ===================================================================== */
/* 3. Health                                                             */
/* ===================================================================== */

/*
 * observability-model.md section 6, unchanged: green is healthy and active,
 * yellow is degraded or correctable before it becomes red, red is failing.
 * Health propagates upward using the worst active state.
 *
 * Three states and no fourth. A node that does not answer is RED, with "no
 * answer" as its evidence - a surface aggregating a cluster says that about
 * the node it could not reach, and an operator reads it the way they read
 * every other red. The owner's call, 2026-09-05: common terminology.
 */
typedef enum {
    XMIP_HEALTH_GREEN  = 0,
    XMIP_HEALTH_YELLOW = 1,
    XMIP_HEALTH_RED    = 2
} XmipHealth;

/*
 * One scope's health, with the evidence behind it. Every state drills down to
 * its evidence; for green the evidence may be empty, for anything else it is
 * the one line an operator reads first. observed is when the runtime took the
 * snapshot, not when the surface asked - a reader that cannot see staleness
 * will eventually mistake a stalled publisher for an idle estate.
 */
typedef struct {
    XmipScope  scope;
    XmipHealth health;
    /*
     * How far from healthy, 0 to 100, shading the colour. The word says which
     * of three states; the number says how bad within it - a yellow at 40 is
     * a backlog worth watching, a yellow at 85 is one about to turn red. What
     * it measures is the publisher's business: backlog against capacity,
     * failures against attempts, latency against threshold. Paused is a
     * category rather than a measurement and publishes a fixed 30. Added
     * 2026-09-05, ADR-0027 amendment.
     */
    uint8_t    severity;
    XmipStr    evidence;
    int64_t    observed_unix_nanos;
} XmipHealthEntry;

/* ===================================================================== */
/* 4. Measurement                                                        */
/* ===================================================================== */

/*
 * What a measurement counts. Never a bare "throughput", because a Stream at a
 * Receive Location, a Journey in an Xmip Process and a Message at a Send
 * Location are three different quantities and Xmip keeps those words apart on
 * every page. ADR-0027 clause 5.
 *
 * BYTES is its own unit; the other three are counts. The record's table lists
 * unit separately; here the counted thing implies it, because there is no
 * measurement that counts Streams in bytes.
 */
typedef enum {
    XMIP_COUNTED_STREAMS  = 1,
    XMIP_COUNTED_MESSAGES = 2,
    XMIP_COUNTED_JOURNEYS = 3,
    XMIP_COUNTED_BYTES    = 4
} XmipCounted;

/*
 * One measurement: a scope, what was counted, the value, the window the value
 * covers, and when it was taken. Cluster and Node figures are sums over the
 * scope tree, and a caller asking for a node gets the sum, not the parts -
 * that is what makes "throughput for every kind of thing" one mechanism.
 */
typedef struct {
    XmipScope   scope;
    XmipCounted counted;
    uint64_t    value;
    int64_t     window_start_unix_nanos;
    int64_t     window_end_unix_nanos;
    int64_t     observed_unix_nanos;
} XmipMeasurement;

/* ===================================================================== */
/* 5. The operator table                                                 */
/* ===================================================================== */

/*
 * What a surface calls. Filled by the runtime, held by the surface, and never
 * the other way round.
 *
 * Every function follows one shape: fill up to cap entries into out, report
 * the true count in out_len whether or not it fit, return XMIP_OK. A surface
 * that passed too small a buffer sees out_len > cap and asks again; nothing
 * is truncated silently. A scope that names nothing returns XMIP_E_NOT_FOUND
 * with out_len 0. Entries borrow from the snapshot and are valid until the
 * next call on this table.
 *
 * There is deliberately no "count now" and no "refresh". A surface reads what
 * was published. If it wants fresher numbers it waits for the publisher.
 */
typedef struct {
    uint32_t abi_version;
    void    *ctx;

    /* Health for the scope and everything beneath it, worst state first. */
    XmipStatus (*health)(void *ctx, XmipScope scope,
                         XmipHealthEntry *out, size_t cap, size_t *out_len);

    /* Measurements for the scope, one per counted kind that has a value. */
    XmipStatus (*measure)(void *ctx, XmipScope scope, XmipCounted counted,
                          XmipMeasurement *out, size_t cap, size_t *out_len);

    /*
     * The first operations on this boundary that act rather than read. Pause
     * everything at and beneath a scope - one Receive Location, one Send
     * Location, a whole stage on a node. While paused it publishes YELLOW,
     * severity 30, evidence "paused by <who>", and its counts stop; resume
     * puts back what was there. XMIP_E_NOT_FOUND when the scope names
     * nothing. `who` is the operator, for the evidence line. Added 2026-09-05,
     * ADR-0027 amendment; Xmip will not always run smoothly, and an operator
     * stopping a Location on purpose is the correctable yellow of
     * observability-model.md section 6, not a fault.
     */
    XmipStatus (*pause)(void *ctx, XmipScope scope, XmipStr who);
    XmipStatus (*resume)(void *ctx, XmipScope scope);

    /* Release the table. After this, nothing borrowed from it is valid. */
    void       (*destroy)(void *ctx);
} XmipOperate;

/*
 * The one symbol an Xmip runtime exports for surfaces. Fills *out and returns
 * XMIP_OK, or returns a status and leaves *out untouched. A runtime that
 * cannot speak the surface's version returns XMIP_E_UNSUPPORTED here rather
 * than failing on the first call.
 */
typedef XmipStatus (*XmipOperateFn)(uint32_t version, XmipOperate *out);

#ifdef __cplusplus
}
#endif

#endif /* XMIP_OPERATE_H */
