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
 * Nothing here asks the hot path. Data calls read a snapshot the runtime
 * published. The optional change signal sleeps the observer until that
 * immutable snapshot advances; publishing only increments a clock and wakes
 * it. ADR-0027 clause 6: the thing that watches cannot stop the thing it watches.
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

#define XMIP_OPERATE_VERSION       1u
#define XMIP_OPERATE_ENTRYPOINT    "xmip_operate_v1"
#define XMIP_WAIT_CHANGE_ENTRYPOINT "xmip_wait_change_v1"

/*
 * The library that exports this boundary stays in the process once loaded.
 * It runs threads of its own that outlive every call - the audit keeper
 * (section 9), a listening Event subscription's (section 11) - so it pins
 * itself as it loads: FreeLibrary or dlclose releases the caller's reference
 * and never unmaps its code. A program may close the library when it is
 * done with it; nothing is unloaded under a thread still running in it.
 */

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
 * location, a work process or a send location. A Party is a filter across
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
 * observability-model.md section 6. Health is a mood, not a colour: it names
 * what a human gets out of a resource under load, and a surface renders it. The
 * leaf moods, worsening: FINE (results flowing), PAUSED (a deliberate hold - an
 * operator is working on it), WORKING (handling the load), STRESSED (strained -
 * change the load), EXHAUSTED (spent - replace hardware), DONE (blocked or
 * failed - the pain: a cert, a password, a missing folder).
 *
 * HOLDING is the rollup mood (ADR-0041). A leaf's mood does NOT propagate: in a
 * perfect world everything is FINE, and the moment anything below is not, the
 * parent is displeased and reports HOLDING - drill in. So a parent is only ever
 * FINE or HOLDING; the leaf carries the real mood, and an operator drills down
 * through the HOLDING scopes to it. FINE up the tree means every leaf beneath is
 * FINE. A node that does not answer is DONE at that node, "no answer" its evidence.
 */
typedef enum {
    XMIP_HEALTH_FINE      = 0,
    XMIP_HEALTH_PAUSED      = 1,
    XMIP_HEALTH_WORKING   = 2,
    XMIP_HEALTH_STRESSED  = 3,
    XMIP_HEALTH_EXHAUSTED = 4,
    XMIP_HEALTH_DONE      = 5,
    XMIP_HEALTH_HOLDING   = 6
} XmipHealth;

/*
 * One scope's health, with the evidence behind it. Every mood drills down to
 * its evidence; for FINE the evidence may be empty, for anything else it is
 * the one line an operator reads first. observed is when the runtime took the
 * snapshot, not when the surface asked - a reader that cannot see staleness
 * will eventually mistake a stalled publisher for an idle estate.
 */
typedef struct {
    XmipScope  scope;
    XmipHealth health;
    /*
     * How far from healthy, 0 to 100, shading the mood within itself. The mood
     * says which; the number says how bad within it - a STRESSED at 40 is a
     * backlog worth watching, a STRESSED at 85 is one close to EXHAUSTED. What
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
 * Receive Location, a Journey in a Work Process and a Message at a Send
 * Location are three different quantities and Xmip keeps those words apart on
 * every page. ADR-0027 clause 5.
 *
 * BYTES is its own unit; the others are counts. RETRYING is the number of
 * work items currently awaiting another attempt; FAILED is the cumulative
 * unsuccessful outcome count for the measurement window. The record's table
 * lists unit separately; here the counted thing implies it, because there is
 * no measurement that counts Streams in bytes.
 */
typedef enum {
    XMIP_COUNTED_STREAMS  = 1,
    XMIP_COUNTED_MESSAGES = 2,
    XMIP_COUNTED_JOURNEYS = 3,
    XMIP_COUNTED_BYTES    = 4,
    XMIP_COUNTED_RETRYING = 5,
    XMIP_COUNTED_FAILED   = 6
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
     * stopping a Location on purpose is a correctable hold
     * (observability-model.md section 6), not a fault.
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

/*
 * Wait until the runtime publishes a revision greater than after_revision.
 * The runtime increments one monotonic process-local revision after replacing
 * its immutable operator snapshot, then wakes every waiter. A timeout is not
 * an error: XMIP_OK is returned and out_revision remains after_revision.
 *
 * This is a separate optional symbol so the version-1 table remains binary
 * compatible during rolling upgrades. Waiting observes only the publication
 * clock and can never delay the execution path.
 */
typedef XmipStatus (*XmipWaitChangeFn)(
    uint64_t after_revision, uint32_t timeout_ms, uint64_t *out_revision);

/* ===================================================================== */
/* 6. Lifecycle: starting and validating a node                          */
/* ===================================================================== */

/*
 * Two more symbols an Xmip runtime exports, for the surfaces that configure a
 * node rather than only watch one - the desktop editor, ADR-0014. Both are the
 * config editor's, not part of the XmipOperate table an observer holds.
 *
 * xmip_start_v1 takes a filesystem PATH to a saved node configuration, reads
 * it, builds and validates the execution tree, and publishes what it planned
 * as health - the running estate then shows it. XMIP_OK when it validated,
 * XMIP_E_INVALID when it did not (the published health says why),
 * XMIP_E_MALFORMED when the path is not UTF-8.
 *
 * xmip_validate_v1 takes the configuration TEXT the editor is holding - not a
 * path, because the point is to check a document before it is saved - reads
 * and validates it, and publishes NOTHING. It is the editor's Validate button:
 * ADR-0027 clause 9, a proposed configuration validated without being applied.
 * The report is written into the caller's buffer as UTF-8, one line per
 * problem, and out_len carries the true byte length whether or not it fit.
 * XMIP_OK and an empty report means the configuration is good; XMIP_E_INVALID
 * with a report means it is not.
 */
typedef XmipStatus (*XmipStartFn)(XmipStr path);
typedef XmipStatus (*XmipValidateFn)(XmipStr configuration,
                                     uint8_t *report, size_t cap, size_t *out_len);

#define XMIP_START_ENTRYPOINT    "xmip_start_v1"
#define XMIP_VALIDATE_ENTRYPOINT "xmip_validate_v1"

/* ===================================================================== */
/* 7. The rules a surface calls instead of keeping its own               */
/* ===================================================================== */

/*
 * A rule, a word list or an order has one implementation, in the crate that
 * owns it, and a surface calls it rather than writing it again - across a
 * language boundary too (ADR-0052, amendment 2026-09-24; ADR-0027, amendment
 * of the same date). Each symbol below is a thin forwarder into its owner:
 *
 *     containment, parts    observe::Scope        (xmip-core-observe)
 *     the wildcard          observe::wildcard     (xmip-core-observe)
 *     stage words,          node::Stage           (xmip-core-node)
 *       pausable, location
 *     role words, a parse   node::NodeRole        (xmip-core-node)
 *     a run's node entry    node::Capability      (xmip-core-node)
 *     mood word, color,     observe::Health       (xmip-core-observe)
 *       the rollup
 *     worst-first order     observe::Standing     (xmip-core-observe)
 *     counted word, the     observe::Counted      (xmip-core-observe)
 *       kind a stage counts
 *     a published           observe::capability   (xmip-core-observe)
 *       capability record
 *
 * None of them reads the snapshot or needs a table: they are pure, callable
 * before, during and without a node, from any thread. Each is a separate
 * optional symbol, like xmip_wait_change_v1, so XMIP_OPERATE_VERSION and the
 * version-1 table are unchanged; a runtime that predates them simply lacks
 * the symbol. Every function returns XMIP_OK, XMIP_E_MALFORMED for text that
 * is not UTF-8, or the status named beside it.
 *
 * Strings handed back either borrow from the caller's own input (valid while
 * that input is) or are static (valid for as long as the library is loaded).
 * Nothing handed back is ever freed by the caller.
 */

/*
 * Whether candidate is scope itself or sits beneath it in the one tree: the
 * scheme and the authority go, a slash at either end is ignored, empty text is
 * the root, and the unit is a segment, never a character. *out_contains is 1
 * or 0.
 */
typedef XmipStatus (*XmipScopeContainsFn)(XmipScope scope, XmipScope candidate,
                                          uint8_t *out_contains);

/*
 * Whether candidate is what pattern names, by the one wildcard (ADR-0052,
 * amendment 2026-09-19): * for any run of characters, ? for exactly one,
 * everything else literal, case-insensitive, * crossing a slash, both sides
 * read as scopes first. A pattern with no wildcard names that one scope, never
 * what is beneath it. *out_matches is 1 or 0.
 */
typedef XmipStatus (*XmipScopeMatchesFn)(XmipScope candidate, XmipStr pattern,
                                         uint8_t *out_matches);

/*
 * Whether something a run made is shown - the run's cluster, its audit
 * records: always where the run declared nothing (hidden 0), and where it
 * declared itself hidden (1) only when the reader asked to include what is
 * hidden (including_hidden 1). The one rule of observe::run::shown (ADR-0028
 * and ADR-0052, amendments 2026-09-30); nothing is read out of a name.
 * *out_shown is 1 or 0.
 */
typedef XmipStatus (*XmipRunShownFn)(uint8_t hidden, uint8_t including_hidden,
                                     uint8_t *out_shown);

/*
 * The segments of a scope's path, top first, in the fill shape of section 5:
 * up to cap entries into out, the true count in out_len. Each entry borrows
 * from scope. The root has none.
 */
typedef XmipStatus (*XmipScopePartsFn)(XmipScope scope,
                                       XmipStr *out, size_t cap, size_t *out_len);

/*
 * Where a scope sits: the node it is on in *out_node, the segment after the
 * node marker beneath the cluster (xmip:///<cluster>/node/<name>, ADR-0053),
 * borrowed from scope; and the stage of the message path it is on in
 * *out_stage, the first stage word beneath its node or, on no node, beneath
 * its cluster, static. Each is empty where there is none: the cluster is
 * never a node, and a cluster's or a node's name is never a stage.
 */
typedef XmipStatus (*XmipScopeNodeFn)(XmipScope scope, XmipStr *out_node,
                                      XmipStr *out_stage);

/*
 * The stage words, in message-path order: the segments a scope names a stage
 * by. Static.
 */
typedef XmipStatus (*XmipStageWordsFn)(XmipStr *out, size_t cap, size_t *out_len);

/*
 * The words a node may declare: its roles, in declaration order (ADR-0056,
 * amendment 2026-10-01). Static.
 */
typedef XmipStatus (*XmipRoleWordsFn)(XmipStr *out, size_t cap, size_t *out_len);

/*
 * The roles a declaration names - words separated by commas or +, each exact
 * lower case - each at most once, in declaration order, and receiving,
 * processing and sending together said as executing, their sum, into roles
 * (static words, the fill shape of section 5). A declaration naming any
 * other word is XMIP_E_INVALID with no role, and the refusal sentence is
 * written into refusal as UTF-8, its true byte length in refusal_len whether
 * or not it fit (ADR-0055: refused by name, never dropped). refusal_len is 0
 * on XMIP_OK.
 */
typedef XmipStatus (*XmipRoleDeclaredFn)(XmipStr declared,
                                         XmipStr *roles, size_t cap, size_t *out_len,
                                         uint8_t *refusal, size_t refusal_cap,
                                         size_t *refusal_len);

/*
 * The stages of the message path a role serves, named by its word (exact
 * lower case), in path order, into out (static words, the fill shape of
 * section 5): one for receiving, processing and sending, all three for
 * executing, none for a role off the path. XMIP_E_NOT_FOUND for a word that
 * is no role.
 */
typedef XmipStatus (*XmipRoleStagesFn)(XmipStr role,
                                       XmipStr *out, size_t cap, size_t *out_len);

/*
 * A mood as the word the estate uses, lower case, and the name of the color a
 * surface paints it in (ADR-0041) - static. XMIP_E_INVALID for a value
 * section 3 does not define.
 */
typedef XmipStatus (*XmipHealthWordFn)(XmipHealth health, XmipStr *out);
typedef XmipStatus (*XmipHealthColorFn)(XmipHealth health, XmipStr *out);

/* The mood a word names, exactly. XMIP_E_NOT_FOUND when it names none. */
typedef XmipStatus (*XmipHealthNamedFn)(XmipStr word, XmipHealth *out);

/*
 * len entries in the worst-first order every reader returns health in - the
 * worse mood, then the higher severity, then the scope in byte order - as
 * their positions: out_order, room for len indices, receives the worst
 * entry's position first, and equals keep the order they came in. Only scope,
 * health and severity are read. One call orders a whole publication, and a
 * surface orders a few things, a pair among them, the same way.
 * XMIP_E_INVALID when a mood is not one section 3 defines.
 */
typedef XmipStatus (*XmipHealthOrderFn)(const XmipHealthEntry *entries, size_t len,
                                        size_t *out_order);

/*
 * What a parent shows when health is the worst mood beneath it (ADR-0041):
 * FINE over FINE, HOLDING over anything else. XMIP_E_INVALID for a mood
 * section 3 does not define.
 */
typedef XmipStatus (*XmipHealthRolledFn)(XmipHealth health, XmipHealth *out);

/*
 * A counted kind as the word the estate uses, lower case - static.
 * XMIP_E_INVALID for a value section 4 does not define.
 */
typedef XmipStatus (*XmipCountedWordFn)(XmipCounted counted, XmipStr *out);

/*
 * Facts of a stage, named by its word (exact lower case): the kind of count
 * the stage takes (STREAMS at receive, JOURNEYS in process, MESSAGES at
 * send), whether an operator may pause it (*out 1 or 0: a Receive or Send
 * Location, never a Process), and what a thing configured at it is called
 * (static). XMIP_E_NOT_FOUND for a word that is no stage.
 */
typedef XmipStatus (*XmipStageCountedFn)(XmipStr stage, XmipCounted *out);
typedef XmipStatus (*XmipStagePausableFn)(XmipStr stage, uint8_t *out);
typedef XmipStatus (*XmipStageLocationFn)(XmipStr stage, XmipStr *out);

/*
 * What a node declared of itself (ADR-0056), read from one health record it
 * published: its name in *out_node (borrowed from scope), its roles in the
 * fill shape (static words, declaration order), and *out_online 1 when it
 * may assume the internet. XMIP_E_NOT_FOUND when the record is not a
 * capability record at all; XMIP_E_INVALID, *out_node still written, when it
 * names a word that is no role, the refusal written as xmip_role_declared_v1
 * writes one.
 */
typedef XmipStatus (*XmipCapabilityPublishedFn)(XmipScope scope, XmipStr evidence,
                                                XmipStr *out_node,
                                                XmipStr *roles, size_t cap,
                                                size_t *out_len, uint8_t *out_online,
                                                uint8_t *refusal, size_t refusal_cap,
                                                size_t *refusal_len);

/*
 * One entry of a run's node list - edge-01=receiving+sending, or a bare name
 * for a node that declared no role: the name in *out_node (borrowed from
 * entry, trimmed) and the roles in the fill shape. XMIP_E_INVALID, *out_node
 * still written, with the refusal, as above.
 */
typedef XmipStatus (*XmipCapabilityEntryFn)(XmipStr entry, XmipStr *out_node,
                                            XmipStr *roles, size_t cap, size_t *out_len,
                                            uint8_t *refusal, size_t refusal_cap,
                                            size_t *refusal_len);

#define XMIP_SCOPE_CONTAINS_ENTRYPOINT "xmip_scope_contains_v1"
#define XMIP_SCOPE_MATCHES_ENTRYPOINT  "xmip_scope_matches_v1"
#define XMIP_RUN_SHOWN_ENTRYPOINT      "xmip_run_shown_v1"
#define XMIP_SCOPE_PARTS_ENTRYPOINT    "xmip_scope_parts_v1"
#define XMIP_SCOPE_NODE_ENTRYPOINT     "xmip_scope_node_v1"
#define XMIP_STAGE_WORDS_ENTRYPOINT    "xmip_stage_words_v1"
#define XMIP_ROLE_WORDS_ENTRYPOINT     "xmip_role_words_v1"
#define XMIP_ROLE_DECLARED_ENTRYPOINT  "xmip_role_declared_v1"
#define XMIP_ROLE_STAGES_ENTRYPOINT    "xmip_role_stages_v1"
#define XMIP_HEALTH_WORD_ENTRYPOINT    "xmip_health_word_v1"
#define XMIP_HEALTH_COLOR_ENTRYPOINT   "xmip_health_color_v1"
#define XMIP_HEALTH_NAMED_ENTRYPOINT   "xmip_health_named_v1"
#define XMIP_HEALTH_ORDER_ENTRYPOINT   "xmip_health_order_v1"
#define XMIP_HEALTH_ROLLED_ENTRYPOINT  "xmip_health_rolled_v1"
#define XMIP_COUNTED_WORD_ENTRYPOINT   "xmip_counted_word_v1"
#define XMIP_STAGE_COUNTED_ENTRYPOINT  "xmip_stage_counted_v1"
#define XMIP_STAGE_PAUSABLE_ENTRYPOINT "xmip_stage_pausable_v1"
#define XMIP_STAGE_LOCATION_ENTRYPOINT "xmip_stage_location_v1"
#define XMIP_CAPABILITY_PUBLISHED_ENTRYPOINT "xmip_capability_published_v1"
#define XMIP_CAPABILITY_ENTRY_ENTRYPOINT     "xmip_capability_entry_v1"

/* ===================================================================== */
/* 8. A publication, read by the runtime                                 */
/* ===================================================================== */

/*
 * A publisher writes a publication - a node's or a roll's snapshot - to a
 * file, and a surface that reads the file reads it here: the runtime parses
 * the text by the one reader there is (observe::Publication, xmip-core-
 * observe) and hands back the header's values, so no surface writes the
 * file's shape again (ADR-0052 and ADR-0027, amendments 2026-09-24).
 *
 * Unlike section 7 this holds something: xmip_publication_read_v1 returns a
 * handle, everything the other calls hand back borrows from it, and
 * xmip_publication_free_v1 releases it and all of that at once. A handle is
 * read-only and may be read from any thread; nothing here touches a running
 * node. Every list follows section 5's fill shape.
 *
 * What the reader does not know it decides once: a mood no one is called is
 * XMIP_HEALTH_STRESSED, so it shows; a counted kind no one is called is
 * skipped; a topology word falls back to COMPUTER, BOTH or SEND_RECEIVE.
 */
typedef struct XmipPublication XmipPublication;

typedef enum {
    XMIP_TOPOLOGY_COMPUTER        = 0,
    XMIP_TOPOLOGY_SERVER          = 1,
    XMIP_TOPOLOGY_VIRTUAL_MACHINE = 2,
    XMIP_TOPOLOGY_GATEWAY         = 3,
    XMIP_TOPOLOGY_APPLIANCE       = 4,
    XMIP_TOPOLOGY_SERVICE         = 5,
    XMIP_TOPOLOGY_PROCESS         = 6,
    XMIP_TOPOLOGY_INTERFACE       = 7,
    XMIP_TOPOLOGY_PORT            = 8,
    XMIP_TOPOLOGY_PROTOCOL        = 9,
    XMIP_TOPOLOGY_LOCATION        = 10,
    XMIP_TOPOLOGY_CLUSTER         = 11,
    XMIP_TOPOLOGY_NODE            = 12,
    XMIP_TOPOLOGY_STAGE           = 13,
    XMIP_TOPOLOGY_ENDPOINT        = 14,
    XMIP_TOPOLOGY_PARTY           = 15
} XmipTopologyKind;

typedef enum {
    XMIP_ORIGIN_CONFIGURED = 0,
    XMIP_ORIGIN_OBSERVED   = 1,
    XMIP_ORIGIN_BOTH       = 2
} XmipTopologyOrigin;

typedef enum {
    XMIP_PATTERN_REQUEST_RESPONSE = 0,
    XMIP_PATTERN_SEND_RECEIVE     = 1,
    XMIP_PATTERN_PUBLISH_CONSUME  = 2,
    XMIP_PATTERN_STREAMING        = 3,
    XMIP_PATTERN_FIRE_AND_FORGET  = 4,
    XMIP_PATTERN_SESSION          = 5,
    XMIP_PATTERN_RETRY            = 6
} XmipCommunicationPattern;

typedef enum {
    XMIP_RUN_TESTS  = 0,
    XMIP_RUN_NODES  = 1,
    XMIP_RUN_ROLES  = 2,
    XMIP_RUN_ONLINE = 3
} XmipRunList;

/*
 * Who published and at which scope, and the single values of the run and
 * the topology: has_run and has_topology are 1 when the publication says
 * either, and the fields beside them are empty when it does not. hidden is
 * 1 where the run declared itself hidden (observe::Run::hidden).
 */
typedef struct {
    XmipStr   source;
    XmipScope node;
    uint8_t   has_run;
    XmipStr   cluster;
    XmipStr   stress;
    uint8_t   hidden;    /* 1 where the run declared itself hidden */
    uint8_t   has_topology;
    XmipStr   topology_source;
    int64_t   topology_observed_unix_nanos;
} XmipPublicationHead;

/* One thing that communicates. parent is empty at the top. */
typedef struct {
    XmipStr     id;
    XmipStr     parent;
    XmipStr     label;
    int32_t     kind;       /* XmipTopologyKind */
    XmipScope   scope;
    XmipHealth  health;
    int32_t     origin;     /* XmipTopologyOrigin */
    double      load;
    double      activity;
    XmipStr     evidence;
} XmipTopologyNode;

/* One communication relationship, from one node id to another. */
typedef struct {
    XmipStr     id;
    XmipStr     from;
    XmipStr     to;
    int32_t     pattern;    /* XmipCommunicationPattern */
    int32_t     origin;     /* XmipTopologyOrigin */
    XmipStr     protocol;
    XmipHealth  health;
    uint64_t    volume;
    double      rate;
    double      latency_ms;
    double      progress;
    uint32_t    attempts;
    XmipStr     evidence;
} XmipTopologyLink;

/*
 * Read a publication's text. XMIP_OK and *out a handle; XMIP_E_INVALID, *out
 * NULL, when the text is not a publication, with the reader's report written
 * into report as xmip_validate_v1 writes one; XMIP_E_MALFORMED when the text
 * is not UTF-8.
 */
typedef XmipStatus (*XmipPublicationReadFn)(XmipStr text, XmipPublication **out,
                                            uint8_t *report, size_t cap, size_t *out_len);
typedef void (*XmipPublicationFreeFn)(XmipPublication *publication);

typedef XmipStatus (*XmipPublicationHeadFn)(const XmipPublication *publication,
                                            XmipPublicationHead *out);
/* The records beneath the publication's scope, worst first. */
typedef XmipStatus (*XmipPublicationRecordsFn)(const XmipPublication *publication,
                                               XmipHealthEntry *out, size_t cap,
                                               size_t *out_len);
/* The counts, each at the scope it was recorded at. */
typedef XmipStatus (*XmipPublicationCountsFn)(const XmipPublication *publication,
                                              XmipMeasurement *out, size_t cap,
                                              size_t *out_len);
typedef XmipStatus (*XmipPublicationNodesFn)(const XmipPublication *publication,
                                             XmipTopologyNode *out, size_t cap,
                                             size_t *out_len);
typedef XmipStatus (*XmipPublicationLinksFn)(const XmipPublication *publication,
                                             XmipTopologyLink *out, size_t cap,
                                             size_t *out_len);
/* One of the run's lists; XMIP_E_INVALID for a list XmipRunList does not name. */
typedef XmipStatus (*XmipPublicationRunFn)(const XmipPublication *publication,
                                           XmipRunList list,
                                           XmipStr *out, size_t cap, size_t *out_len);

#define XMIP_PUBLICATION_READ_ENTRYPOINT    "xmip_publication_read_v1"
#define XMIP_PUBLICATION_FREE_ENTRYPOINT    "xmip_publication_free_v1"
#define XMIP_PUBLICATION_HEAD_ENTRYPOINT    "xmip_publication_head_v1"
#define XMIP_PUBLICATION_RECORDS_ENTRYPOINT "xmip_publication_records_v1"
#define XMIP_PUBLICATION_COUNTS_ENTRYPOINT  "xmip_publication_counts_v1"
#define XMIP_PUBLICATION_NODES_ENTRYPOINT   "xmip_publication_nodes_v1"
#define XMIP_PUBLICATION_LINKS_ENTRYPOINT   "xmip_publication_links_v1"
#define XMIP_PUBLICATION_RUN_ENTRYPOINT     "xmip_publication_run_v1"

/*
 * What a topology value is called (observe::topology): the word a
 * publication writes it as in *out_word, and the name a person reads it by -
 * under a node, in a legend, in an inspector's row - in *out_name. Both
 * static, and pure like section 7: no handle, any thread, before any node.
 * The kind takes an XmipTopologyKind, the origin an XmipTopologyOrigin, the
 * pattern an XmipCommunicationPattern; XMIP_E_INVALID for a value its enum
 * does not define. A surface asks here rather than keeping a word list of its
 * own (ADR-0052, amendment 2026-09-25).
 */
typedef XmipStatus (*XmipTopologyWordsFn)(int32_t value, XmipStr *out_word,
                                          XmipStr *out_name);

#define XMIP_TOPOLOGY_KIND_WORDS_ENTRYPOINT   "xmip_topology_kind_words_v1"
#define XMIP_TOPOLOGY_ORIGIN_WORDS_ENTRYPOINT "xmip_topology_origin_words_v1"
#define XMIP_TOPOLOGY_PATTERN_WORDS_ENTRYPOINT "xmip_topology_pattern_words_v1"

/*
 * A curve: a node's throughput over time, as the file a publisher writes
 * beside its publication (ADR-0029), read by the one reader there is
 * (observe::Curve). The same handle rules as above: xmip_curve_read_v1 hands
 * back a handle, or XMIP_E_INVALID with the reader's report; the points
 * borrow from it, each an XmipMeasurement at the curve's node whose window is
 * the instant it was observed, oldest first within a kind; a counted kind no
 * one is called is skipped; xmip_curve_free_v1 releases it.
 */
typedef struct XmipCurve XmipCurve;

typedef XmipStatus (*XmipCurveReadFn)(XmipStr text, XmipCurve **out,
                                      uint8_t *report, size_t cap, size_t *out_len);
typedef XmipStatus (*XmipCurvePointsFn)(const XmipCurve *curve,
                                        XmipMeasurement *out, size_t cap, size_t *out_len);
typedef void (*XmipCurveFreeFn)(XmipCurve *curve);

#define XMIP_CURVE_READ_ENTRYPOINT   "xmip_curve_read_v1"
#define XMIP_CURVE_POINTS_ENTRYPOINT "xmip_curve_points_v1"
#define XMIP_CURVE_FREE_ENTRYPOINT   "xmip_curve_free_v1"

/* ===================================================================== */
/* 9. A program's audit record                                           */
/* ===================================================================== */

/*
 * Every Xmip program audits through the audit capability, xmip-core-audit
 * (ADR-0062): what it started and stopped, every act an operator took through
 * it, and every failure. A program that is not Rust records here, and this is
 * a thin forwarder into audit::program_audit::ProgramAudit - the record, its
 * policy, its sink and the fallback are the capability's, never the caller's.
 *
 * program names the program (Xmip.Gui.Web, xmip-cli); directory is where its
 * records go when the caller was told one (empty: the capability decides -
 * XMIP_AUDIT_DIRECTORY, else the operating system's log). action is what it
 * did, message what it says about it, empty for none. properties holds
 * properties_len strings, key then value, so properties_len is even.
 *
 * *out_kept says what became of it. XMIP_KEPT_OPERATING_SYSTEM means the
 * sink could not keep it and the operating system's log does (ADR-0062
 * clause 3); said then holds where and why, as UTF-8, its true byte length in
 * said_len whether or not it fit, and said_len is 0 when it went to the sink.
 * A failure - XMIP_PHASE_FAILURE, or XMIP_SEVERITY_ERROR at any phase - is
 * always recorded; that is not policy.
 *
 * XMIP_E_INVALID for a phase or severity not defined here or an odd
 * properties_len; XMIP_E_IO, with the reasons in said, when neither the sink
 * nor the operating system's log kept it. Pure in clause 6's sense: it reads
 * no snapshot, may be called from any thread, before, during and without a
 * node, and holds nothing afterwards. A separate optional symbol, as section
 * 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef enum {
    XMIP_PHASE_BEGIN    = 0,
    XMIP_PHASE_EXECUTE  = 1,
    XMIP_PHASE_FINISHED = 2,
    XMIP_PHASE_FAILURE  = 3
} XmipPhase;

typedef enum {
    XMIP_SEVERITY_INFORMATION = 0,
    XMIP_SEVERITY_WARNING     = 1,
    XMIP_SEVERITY_ERROR       = 2
} XmipSeverity;

typedef enum {
    XMIP_KEPT_SUPPRESSED       = 0,
    XMIP_KEPT_PERSISTED        = 1,
    XMIP_KEPT_OPERATING_SYSTEM = 2
} XmipKept;

typedef XmipStatus (*XmipAuditFn)(XmipStr program, XmipStr directory, XmipStr action,
                                  XmipPhase phase, XmipSeverity severity, XmipStr message,
                                  const XmipStr *properties, size_t properties_len,
                                  XmipKept *out_kept,
                                  uint8_t *said, size_t said_cap, size_t *said_len);

#define XMIP_AUDIT_ENTRYPOINT "xmip_audit_v1"

/*
 * The audit read back (ADR-0062, amendment 2026-09-29): what every surface
 * asks of the records programs audited, answered by the audit capability's
 * one reader and one query (audit::audit_query::AuditQuery). A thin
 * forwarder, pure like section 7: no handle, any thread, before any node.
 *
 * directory is the audit directory to read, by the rule xmip_audit_v1 writes
 * by (empty: XMIP_AUDIT_DIRECTORY, else there is none to read). query holds
 * query_len strings, key then value: pattern (the wildcard of section 7 over
 * each record's location; a record with none is at the root, which only *
 * names), location (at and beneath this scope), host (records that declared
 * no location, on this host), program, record (one audit_id; every other
 * filter set aside), severity, action, from and to (RFC 3339; a date, or a
 * date and time with no zone, is UTC), sort (at, location, node, program,
 * host, action, phase, severity, summary), order (ascending, descending; the
 * default is descending), offset and limit (at most 1000; 100 when unstated),
 * hidden (exclude, include) and verify (no, yes: walk the audit chain of each
 * writer of the records matched, ADR-0070 clause 5). An empty value is no
 * filter. The answer is JSON, in memory only (ADR-0031
 * clause 2), written into out as UTF-8, its true byte length in out_len
 * whether or not it fit:
 *
 *   {"file":"<the file read, empty for none>","read":<records it holds>,
 *    "matched":<records the query matched>,"offset":<n>,"limit":<n>,
 *    "records":[{"audit_id","at","program","host","process","location"?,
 *                "node"?,"cluster"?,"action","phase","severity","message"?,
 *                "summary","scope":{...},"properties":{...}}],
 *    "groups":[{"kind":"cluster"|"node"|"scope"|"program"|"host","who",
 *               "count","warnings","errors","latest"}],
 *    "actions":["<every action where the query stands>"],
 *    "chains":[{"writer","records","whole","said"}],
 *    "columns":["at",...],"severities":["information","warning","error"]}
 *
 * groups are one step down from where the query stands - clusters and hosts
 * at the top, a cluster's nodes and its own programs, a node's programs -
 * and none once a program is asked; chains are empty unless verify is yes,
 * and then each writer's chain - a node's location, or a program's name -
 * walked whole over every record of it, hidden ones among them, with the
 * first place it breaks (a record deleted, changed or out of order) or
 * that it is whole, in one sentence opening OK or FAILED; columns are the
 * sort words in the
 * order a reader shows them and severities least first, so no surface
 * keeps a list of its own. XMIP_OK with the answer;
 * XMIP_E_INVALID with the refusal, one sentence opening REFUSED, in out for
 * a key or value the query does not take or an odd query_len; XMIP_E_IO
 * with the reason when the file is there and cannot be read;
 * XMIP_E_MALFORMED when a string is not UTF-8. Optional symbol, as section
 * 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipAuditReadFn)(XmipStr directory,
                                      const XmipStr *query, size_t query_len,
                                      uint8_t *out, size_t cap, size_t *out_len);

#define XMIP_AUDIT_READ_ENTRYPOINT "xmip_audit_read_v1"

/*
 * The event source every Xmip entry in the Windows Event Log is written under
 * when audit cannot persist a record (ADR-0062 clause 3): the audit
 * capability's writer, a program's own entry when it cannot reach audit, and
 * the prerequisite installer that registers the source all read it here.
 */
#define XMIP_EVENT_SOURCE "Xmip"

/*
 * The sentence an entry opens with when XMIP_EVENT_SOURCE is not registered
 * and the entry is written under the Application log's .NET Runtime source
 * instead. One line, so every reader of this header takes it whole.
 */
#define XMIP_EVENT_SOURCE_UNREGISTERED "The Xmip event source is not registered and registering it needs elevation once (Install-XmipPrerequisite does it), so this is written under the .NET Runtime source."

/* ===================================================================== */
/* 10. The cluster's xmip.toml, read, edited and sliced                  */
/* ===================================================================== */

/*
 * A developer and an operator work on one xmip.toml per cluster, and the
 * VS Code designer is a view of its sections, artifact by artifact
 * (ADR-0064, amendment 2026-10-03). The designer holds no rule: its language
 * server asks here, and each export is a thin forwarder into
 * xmip-core-configure, where the file, its views, an Xmip Application's
 * routes, its filters, every edit and the one slicing are read. The
 * Operation Desktop's Configure page asks the same five (ADR-0031,
 * amendment 2026-10-05: only the cluster's xmip.toml is edited, and saving
 * it slices it for each node). Pure like section 7: no handle, any thread,
 * before any node. Validating the file is xmip_validate_v1's, node by node.
 *
 * One shape for the five. input is the text the export reads; argument is
 * the edit xmip_cluster_edit_v1 makes, the node xmip_cluster_slices_v1
 * slices for (empty: every node), and empty for the others. The answer
 * is written into out as UTF-8, its true byte length in out_len whether or
 * not it fit, as xmip_validate_v1 writes its report. XMIP_OK with the
 * answer; XMIP_E_INVALID with the refusal, one sentence, in out;
 * XMIP_E_MALFORMED when input or argument is not UTF-8.
 *
 * What crosses is JSON, in memory only (ADR-0031 clause 2):
 *   xmip_cluster_views_v1     a cluster's xmip.toml in; one view per
 *                             artifact kind out (kind, title, defined, note,
 *                             entries, places): each entry's section path,
 *                             name, scope and fields, and an Xmip
 *                             Application's routes as a graph — nodes (id,
 *                             kind, name, column, target, a Subscription's
 *                             filter and summary), edges (from, to, kind),
 *                             problems, and the operators and kinds a filter
 *                             row offers; and document, "cluster" where the
 *                             text declares its nodes, "node" where it is a
 *                             node's own document, which no tool edits.
 *   xmip_filter_structure_v1  a filter's text in; its rows and groups out.
 *   xmip_filter_text_v1       rows and groups in; the filter's canonical
 *                             text out, which reads back byte for byte.
 *   xmip_cluster_edit_v1      a cluster's xmip.toml in, an edit as
 *                             argument; the edited text out, everything the
 *                             edit does not touch as it was.
 *   xmip_cluster_slices_v1    a cluster's xmip.toml in, a node's name or
 *                             empty as argument; out
 *                             {"slices":[{"node","text"}]}, each node's
 *                             configuration document as configure::slice
 *                             writes it, in the order of the node names.
 */
typedef XmipStatus (*XmipDesignFn)(XmipStr input, XmipStr argument,
                                   uint8_t *out, size_t cap, size_t *out_len);

#define XMIP_CLUSTER_VIEWS_ENTRYPOINT    "xmip_cluster_views_v1"
#define XMIP_FILTER_STRUCTURE_ENTRYPOINT "xmip_filter_structure_v1"
#define XMIP_FILTER_TEXT_ENTRYPOINT      "xmip_filter_text_v1"
#define XMIP_CLUSTER_EDIT_ENTRYPOINT     "xmip_cluster_edit_v1"
#define XMIP_CLUSTER_SLICES_ENTRYPOINT   "xmip_cluster_slices_v1"

/* ===================================================================== */
/* 11. Events, subscribed                                                */
/* ===================================================================== */

/*
 * Every completed Receive, Process and Send action produces an Event for
 * every outcome (runtime-model.md section 17), and a program in any language
 * subscribes to them here (ADR-0065). Each export is a thin forwarder into
 * xmip-core-event: which Events a filter matches, whether the subscriber may
 * see them, the queue and the audit are written there once, and no binding
 * decides any of it again.
 *
 * A subscriber is a Party: subscriber is the Party's UUID, and the program
 * that loaded this library is its identity - the operating system vouches
 * for a caller in this process, and the authorization gate decides what it
 * may see: being in this process admits it to nothing
 * (xmip_event_authorize_v1 below). program and directory say where its subscription, its deliveries
 * and its refusals are audited, as section 9's do (empty directory: the
 * capability decides). A refused subscription is XMIP_E_AUTH, *out NULL,
 * with the gate's sentence in said as xmip_validate_v1 writes its report;
 * an empty subscriber or an outcome not defined here is XMIP_E_INVALID, a
 * subscriber or filter Party that is not a UUID, or text that is not UTF-8,
 * XMIP_E_MALFORMED.
 *
 * A filter's lists and strings, each empty for any: the Event types, the
 * outcomes, the scope the Event must have happened at or beneath (an Xmip
 * URI, section 2), and the Party's UUID it must be about. capacity bounds the
 * subscription's queue, 0 for the default; publishing never waits for a
 * subscriber, and an Event a full queue refused is counted, handed over as
 * *out_refused at the next drain, and audited.
 *
 * Draining, section 8's handle pattern: xmip_event_next_v1 waits up to
 * timeout_ms for the first Event, waking the moment one arrives, and hands
 * over up to max of them (at least one; max 0 is one) as a batch -
 * *out_events points at *out_len XmipEvents whose every string
 * borrows from *out_batch - and xmip_event_batch_free_v1 releases the batch
 * and all of it. XMIP_OK with a batch when anything arrived or was refused;
 * XMIP_E_TIMEOUT, *out_batch NULL, when nothing did; XMIP_E_STATE on a
 * listening subscription, which is never drained. A handle is drained from
 * one thread at a time, and never unsubscribed while a next on it has not
 * returned.
 *
 * Called back instead: xmip_event_listen_v1 subscribes as subscribe does and
 * calls callback with context for each Event, on a thread the runtime starts
 * for this subscription and never on the publisher's; the Event and its
 * strings are valid for that call only. One callback at a time per
 * subscription. XMIP_E_CAPACITY when the thread could not be started.
 *
 * xmip_event_unsubscribe_v1 releases either kind of handle. For a listening
 * one it returns once the callback in progress has returned - except when it
 * is called from inside that callback, where it returns at once and the
 * thread ends after the callback does. It returns once the audit records
 * of the subscription are kept, so a program that unsubscribed and exits
 * has lost none. Nothing borrowed from a handle is valid afterwards; a batch
 * drained before stays valid until it is freed.
 *
 * xmip_event_publish_v1 hands an Event of the caller's to every matching
 * subscription and says in *out_delivered how many queues took it. An empty
 * id is minted, a time of 0 is now; the type and scope must not be empty
 * and diagnostics_len, a count of strings, must be even (XMIP_E_INVALID, as
 * for an action or outcome not defined here), and an identifier that is not
 * a UUID, or text that is not UTF-8, is XMIP_E_MALFORMED.
 *
 * Every call may be made from any thread, before, during and without a
 * node; it touches no snapshot (ADR-0027 clause 6). Separate optional
 * symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef enum {
    XMIP_ACTION_RECEIVE = 0,
    XMIP_ACTION_PROCESS = 1,
    XMIP_ACTION_SEND    = 2
} XmipAction;

typedef enum {
    XMIP_OUTCOME_SUCCESS           = 0,
    XMIP_OUTCOME_FAILURE           = 1,
    XMIP_OUTCOME_REJECTION         = 2,
    XMIP_OUTCOME_WAITING           = 3,
    XMIP_OUTCOME_PAUSE             = 4,
    XMIP_OUTCOME_TIMEOUT           = 5,
    XMIP_OUTCOME_EXHAUSTED_RETRIES = 6,
    XMIP_OUTCOME_DISMISSAL         = 7
} XmipOutcome;

/*
 * One Event: references, never a payload. Identifiers are UUIDs in
 * 8-4-4-4-12 form, and every optional string is empty where there is none.
 */
typedef struct {
    XmipStr        id;
    XmipStr        type;
    int64_t        time_unix_nanos;
    int32_t        action;          /* XmipAction */
    int32_t        outcome;         /* XmipOutcome */
    XmipScope      scope;           /* where it happened */
    XmipStr        journey;
    XmipStr        message;
    XmipStr        stream;
    XmipStr        endpoint;
    XmipStr        module;
    XmipStr        artifact;
    XmipStr        party;           /* the Party it is about */
    const XmipStr *diagnostics;     /* name then value, safe to hand outside */
    size_t         diagnostics_len;
} XmipEvent;

typedef struct {
    const XmipStr *types;
    size_t         types_len;
    const int32_t *outcomes;        /* XmipOutcome */
    size_t         outcomes_len;
    XmipScope      scope;
    XmipStr        party;
} XmipEventFilter;

typedef struct XmipEventSubscription XmipEventSubscription;
typedef struct XmipEventBatch XmipEventBatch;

typedef void (*XmipEventCallback)(void *context, const XmipEvent *event);

typedef XmipStatus (*XmipEventSubscribeFn)(XmipStr program, XmipStr directory,
                                           XmipStr subscriber, const XmipEventFilter *filter,
                                           size_t capacity, XmipEventSubscription **out,
                                           uint8_t *said, size_t said_cap, size_t *said_len);
typedef XmipStatus (*XmipEventNextFn)(XmipEventSubscription *subscription, uint32_t timeout_ms,
                                      size_t max, XmipEventBatch **out_batch,
                                      const XmipEvent **out_events, size_t *out_len,
                                      uint64_t *out_refused);
typedef void (*XmipEventBatchFreeFn)(XmipEventBatch *batch);
typedef XmipStatus (*XmipEventListenFn)(XmipStr program, XmipStr directory,
                                        XmipStr subscriber, const XmipEventFilter *filter,
                                        size_t capacity, XmipEventCallback callback,
                                        void *context, XmipEventSubscription **out,
                                        uint8_t *said, size_t said_cap, size_t *said_len);
typedef void (*XmipEventUnsubscribeFn)(XmipEventSubscription *subscription);
typedef XmipStatus (*XmipEventPublishFn)(const XmipEvent *event, size_t *out_delivered);

#define XMIP_EVENT_SUBSCRIBE_ENTRYPOINT   "xmip_event_subscribe_v1"
#define XMIP_EVENT_NEXT_ENTRYPOINT        "xmip_event_next_v1"
#define XMIP_EVENT_BATCH_FREE_ENTRYPOINT  "xmip_event_batch_free_v1"
#define XMIP_EVENT_LISTEN_ENTRYPOINT      "xmip_event_listen_v1"
#define XMIP_EVENT_UNSUBSCRIBE_ENTRYPOINT "xmip_event_unsubscribe_v1"
#define XMIP_EVENT_PUBLISH_ENTRYPOINT     "xmip_event_publish_v1"

/*
 * Who may subscribe (ADR-0065, amendment 2026-09-26): a program in this
 * process is admitted to nothing for being here, and who may subscribe is a
 * policy and nothing beside it. The program hosting this library hands the
 * process's hub its policy with xmip_event_authorize_v1, as a node hands its
 * own as it starts: decide is asked of each attempt to subscribe - once per
 * Event type the filter names - with the accountable identity's Party UUID
 * (empty where it resolved to none), mechanism and value, and the attempt's
 * artifact (the scope the filter reaches) and Contract (the Event type,
 * empty for every type), and answers XMIP_EVENT_ALLOW, XMIP_EVENT_DENY or
 * XMIP_EVENT_NO_OPINION. It replaces the policy handed before; a null decide
 * hands none. Nothing having an opinion is a refusal: until a policy allows,
 * every subscription is XMIP_E_AUTH. decide is called with context, on the
 * subscribing thread, until it is replaced; it must be safe to call from any
 * thread. XMIP_OK.
 *
 * Nothing missing is silent (ADR-0065, amendment 2026-10-02). A subscriber
 * on any node hears the Events of every node of the cluster; a member this
 * node cannot hear now is unheard until its link is made again, and its
 * Events meanwhile are not among any delivered. Every batch says who:
 * xmip_event_batch_unheard_v1 writes, as JSON in memory, the same shape the
 * subscriptions list's "unheard" has, with "changed" true when the members
 * not heard changed since the drain before -
 *
 *   {"changed":true,"unheard":[{"by","node","since_unix_nanos","why","said"}]}
 *
 * by the node that does not hear, node the member, since_unix_nanos since
 * when, why in words, and said the one line every surface shows: "not
 * hearing <node> since <time>: <why>". A change wakes a waiting
 * xmip_event_next_v1 with XMIP_OK and a batch of no Events, so a drained
 * subscriber learns of it at once. A listening subscription, which has no
 * batch, asks xmip_event_unheard_v1, the hub's list without "changed".
 * Both write into out, out_len its true length whether or not it fit;
 * XMIP_OK, or XMIP_E_INVALID for no batch.
 */
typedef enum {
    XMIP_EVENT_DENY       = -1,
    XMIP_EVENT_NO_OPINION = 0,
    XMIP_EVENT_ALLOW      = 1
} XmipEventDecision;

typedef int32_t (*XmipEventAuthorizerFn)(void *context, XmipStr party, XmipStr mechanism,
                                         XmipStr value, XmipScope artifact, XmipStr contract);
typedef XmipStatus (*XmipEventAuthorizeFn)(XmipEventAuthorizerFn decide, void *context);
typedef XmipStatus (*XmipEventBatchUnheardFn)(const XmipEventBatch *batch, uint8_t *out,
                                              size_t cap, size_t *out_len);
typedef XmipStatus (*XmipEventUnheardFn)(uint8_t *out, size_t cap, size_t *out_len);

#define XMIP_EVENT_AUTHORIZE_ENTRYPOINT     "xmip_event_authorize_v1"
#define XMIP_EVENT_BATCH_UNHEARD_ENTRYPOINT "xmip_event_batch_unheard_v1"
#define XMIP_EVENT_UNHEARD_ENTRYPOINT       "xmip_event_unheard_v1"

/*
 * What an operator lists and does (ADR-0065, amendment 2026-09-29): every
 * Event subscription a hub holds, and pause, resume and remove on one of
 * them. Thin forwarders into xmip-core-event, pure like section 7: no
 * handle, any thread, before, during and without a node. Who may act is the
 * surface's to decide by role; the hub applies what reaches it and audits it
 * in the subscriber's audit, with who acted. An Event subscription is not a
 * Subscription, which picks a published Message up (section 14).
 *
 * A list is JSON, in memory only (ADR-0031 clause 2), written into out as
 * UTF-8, its true byte length in out_len whether or not it fit:
 *
 *   {"orders":"<where acts are left, empty for none>",
 *    "event_subscriptions":[{"node","id","subscriber","party","action","scope",
 *                            "state","paused","queued","capacity","delivered",
 *                            "missed","since_unix_nanos"}],
 *    "unheard":[{"by","node","since_unix_nanos","why","said"}]}
 *
 * node is the scope of the node whose hub holds it and id its number there,
 * the two naming it; subscriber is the name the Party was declared with,
 * empty where it was declared with none, and party its UUID; action what its filter
 * asks for, in words (every Event, or the types, then the outcomes); scope
 * what its filter reaches; state active or paused, and paused true when it
 * is, so no reader keeps the words; queued, capacity,
 * delivered and missed its queue's counts, missed being what a full queue
 * refused since it was made. unheard is every member of the cluster a node
 * does not hear now (above, xmip_event_batch_unheard_v1): an Event
 * subscription there hears every other node's Events but those. The links
 * that carry Events between nodes are the cluster's, never listed and never
 * acted on.
 *
 * xmip_event_subscriptions_v1 lists this process's hub, each entry at node,
 * the scope the caller says this process publishes at; orders is empty.
 * XMIP_OK; XMIP_E_MALFORMED when node is not UTF-8.
 *
 * xmip_event_subscription_act_v1 applies act - pause, resume or remove,
 * exact - to Event subscription id in this process's hub, by who. Paused, it
 * keeps queuing up to its capacity and hands nothing over, a full queue
 * counting what it refused as missed; resumed, it hands over what queued;
 * removed, it is unsubscribed and its holder's next drain finds it closed.
 * XMIP_OK with what came of it in said, one sentence; XMIP_E_NOT_FOUND with
 * the refusal, opening REFUSED, when no subscription of that number is held;
 * XMIP_E_INVALID with the refusal for a word that is no act.
 *
 * A surface reading a publication touches no node: it leaves the act where
 * the publication says (orders) through section 14's xmip_order_v1, noun
 * event-subscription, target the number.
 *
 * xmip_publication_event_subscriptions_v1 lists what a read publication
 * (section 8) carries, orders as it says. XMIP_E_INVALID for no handle.
 *
 * Every string may be empty. Optional symbols, as section 7's are;
 * XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipEventSubscriptionsFn)(XmipScope node, uint8_t *out, size_t cap,
                                               size_t *out_len);
typedef XmipStatus (*XmipEventSubscriptionActFn)(uint64_t id, XmipStr act, XmipStr who,
                                                 uint8_t *said, size_t said_cap,
                                                 size_t *said_len);
typedef XmipStatus (*XmipPublicationEventSubscriptionsFn)(const XmipPublication *publication,
                                                          uint8_t *out, size_t cap,
                                                          size_t *out_len);

#define XMIP_EVENT_SUBSCRIPTIONS_ENTRYPOINT             "xmip_event_subscriptions_v1"
#define XMIP_EVENT_SUBSCRIPTION_ACT_ENTRYPOINT          "xmip_event_subscription_act_v1"
#define XMIP_PUBLICATION_EVENT_SUBSCRIPTIONS_ENTRYPOINT "xmip_publication_event_subscriptions_v1"

/* ===================================================================== */
/* 12. The technologies a runtime carries, and what each declares        */
/* ===================================================================== */

/*
 * A Receive or Send Location's form is never written in a surface: every
 * technology declares its own settings in its own crate - each setting's
 * name, kind, default, whether it is required, what it means and which side
 * reads it - and that one declaration is what the technology reads its
 * settings through and what xmip_validate_v1 and xmip_start_v1 hold a
 * Location to (ADR-0064, amendment 2026-09-26). The language server and the
 * desktop editor read it here. A thin forwarder, pure like section 7: no
 * handle, any thread, before any node.
 *
 * technology empty for every technology the runtime carries, or a module
 * name (xmip-core-transport-kafka) for that one alone. The answer is JSON,
 * in memory only (ADR-0031 clause 2), written into out as UTF-8, its true
 * byte length in out_len whether or not it fit:
 *
 *   {"technologies":[{"capability":"transport"|"contract",
 *     "technology":"<module name>",
 *     "settings":[{"name","kind","presence","default"?,"meaning","applies",
 *                  "minimum"?,"maximum"?,"choices"?}]}]}
 *
 * kind is text, integer (minimum, maximum), boolean, duration (a whole
 * number and ms, s, m or h), address, secret (the name of a secret, never
 * the secret) or choice (choices); presence is required, optional or
 * default (default); applies is receive, send or both. XMIP_OK with the
 * answer; XMIP_E_INVALID with the refusal, one sentence, when the runtime
 * carries no technology of that name; XMIP_E_MALFORMED when it is not UTF-8.
 * Optional symbol, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipCatalogueFn)(XmipStr technology, uint8_t *out,
                                      size_t cap, size_t *out_len);

#define XMIP_TECHNOLOGY_CATALOGUE_ENTRYPOINT "xmip_technology_catalogue_v1"

/* ===================================================================== */
/* 13. A System Process, declared                                        */
/* ===================================================================== */

/*
 * Every System Process Xmip owns says of itself its name, its location and
 * its purpose, test or runtime, to one file named for it and its pid in the
 * directory XMIP_PROCESS_DIRECTORY names, else xmip/process under the
 * system's temporary directory (ADR-0053 clause 3). The file, its directory
 * and its words are xmip-core-node's; a .NET program declares itself and the
 * estate's tooling lists the declarations through these two thin forwarders,
 * and writes or reads no file of its own. Pure like section 7: no handle,
 * any thread, before any node.
 *
 * xmip_process_declare_v1 declares the calling process: name (xmip-<what>),
 * location, purpose ("test" or "runtime", exact), and properties_len strings
 * in properties, key then value, for what else it says. The file it wrote
 * is written into out as UTF-8, its true byte length in out_len whether or
 * not it fit; the declaration stands until the caller removes that file.
 * XMIP_OK with the file; XMIP_E_INVALID with the refusal, one sentence, in
 * out when the purpose is no purpose word or a key is not a bare word or is
 * one of the six every declaration writes; XMIP_E_IO with the reason when
 * the file could not be written; XMIP_E_MALFORMED when a string is not UTF-8.
 *
 * xmip_process_declarations_v1 reads every declaration standing in
 * directory, empty for the directory the rule above names, whether or not
 * its process still runs: that is the reader's to judge. The answer is JSON,
 * in memory only (ADR-0031 clause 2), written into out as UTF-8, its true
 * byte length in out_len whether or not it fit:
 *
 *   {"directory":"<the directory read>",
 *    "processes":[{"file","name","location","purpose","pid","started_unix",
 *                  "path","said":{"<key>":"<value>",...}}]}
 *
 * XMIP_OK with the answer; XMIP_E_MALFORMED when directory is not UTF-8.
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipProcessDeclareFn)(XmipStr name, XmipStr location, XmipStr purpose,
                                           const XmipStr *properties, size_t properties_len,
                                           uint8_t *out, size_t cap, size_t *out_len);

typedef XmipStatus (*XmipProcessDeclarationsFn)(XmipStr directory, uint8_t *out,
                                                size_t cap, size_t *out_len);

#define XMIP_PROCESS_DECLARE_ENTRYPOINT      "xmip_process_declare_v1"
#define XMIP_PROCESS_DECLARATIONS_ENTRYPOINT "xmip_process_declarations_v1"

/* ===================================================================== */
/* 14. A node's Subscriptions, seen and paused; an operator's order      */
/* ===================================================================== */

/*
 * A Subscription picks a published Message up and opens a Journey (ADR-0013).
 * It is configuration: drawn in an Xmip Application, bound in a node's TOML,
 * and added and removed there and nowhere else. An operator lists every
 * Subscription and pauses or resumes one; there is no remove (ADR-0013,
 * amendment 2026-09-30). Thin forwarders into the runtime's pickup, pure
 * like section 7: no handle, any thread, before, during and without a node.
 * Who may act is the surface's to decide by role; the node applies what
 * reaches it and audits it with who acted.
 *
 * Paused, every Message routing matches to the Subscription is held: kept
 * in the node's runtime store, numbered in the order held and counted, and
 * not picked up; nothing is lost. Resumed, what it held is picked up oldest
 * first, and what it matches meanwhile joins the end. A pause, and what it
 * holds, survives a restart of the node.
 *
 * A list is JSON, in memory only (ADR-0031 clause 2), written into out as
 * UTF-8, its true byte length in out_len whether or not it fit:
 *
 *   {"orders":"<where acts are left, empty for none>",
 *    "subscriptions":[{"node","name","application","filter","destination",
 *                      "file","configuration","state","paused","by",
 *                      "picked_up","held","since_unix_nanos"}]}
 *
 * node is the scope of the node that routes by it and name its configured
 * name, the two naming it; application the Xmip Application that draws it,
 * file the node configuration that Application is a section of and
 * configuration its [[xmip_applications.subscriptions]] entry there as the
 * file says it; filter what it
 * subscribes to, as configured; destination where it leads, in words; state
 * active or paused, paused true when it is, and by who paused it; picked_up
 * what it picked up since the node started and held what it holds now;
 * since when its state began.
 *
 * xmip_subscriptions_v1 lists the Subscriptions of every node running in
 * this process at or beneath node (empty: every one); orders is empty.
 * XMIP_OK; XMIP_E_MALFORMED when node is not UTF-8.
 *
 * xmip_subscription_act_v1 applies act - pause or resume, exact - to the
 * Subscription name of the node at node in this process, by who. XMIP_OK
 * with what came of it in said, one sentence; XMIP_E_NOT_FOUND with the
 * refusal, opening REFUSED, when that node does not run here or is not
 * configured with that Subscription; XMIP_E_INVALID with the refusal for a
 * word that is no act on a Subscription - remove among them, whose refusal
 * says a Subscription is removed in the TOML configuration.
 *
 * xmip_publication_subscriptions_v1 lists what a read publication (section
 * 8) carries, orders as it says. XMIP_E_INVALID for no handle.
 *
 * A surface reading a publication touches no node. Where the publication
 * says where its publisher takes orders, xmip_order_v1 leaves act on the
 * noun called target of the node at node there - noun subscription with a
 * Subscription's name, event-subscription with an Event subscription's
 * number, dead-message with a Message's identifier and the act replay
 * (section 15), or journey with a Journey's identifier and the act retry or
 * dismiss (section 16) - for the node to take at its next look and apply; said
 * holds the file written. XMIP_OK; XMIP_E_INVALID with the refusal for an
 * empty orders, a node that names no node, a noun that is no noun, or an
 * act the noun does not take; XMIP_E_IO with the reason when it could not
 * be written.
 *
 * Every string may be empty. Optional symbols, as section 7's are;
 * XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipSubscriptionsFn)(XmipScope node, uint8_t *out, size_t cap,
                                          size_t *out_len);
typedef XmipStatus (*XmipSubscriptionActFn)(XmipScope node, XmipStr name, XmipStr act,
                                            XmipStr who, uint8_t *said, size_t said_cap,
                                            size_t *said_len);
typedef XmipStatus (*XmipPublicationSubscriptionsFn)(const XmipPublication *publication,
                                                     uint8_t *out, size_t cap,
                                                     size_t *out_len);
typedef XmipStatus (*XmipOrderFn)(XmipStr orders, XmipScope node, XmipStr noun,
                                  XmipStr target, XmipStr act, XmipStr who, uint8_t *said,
                                  size_t said_cap, size_t *said_len);

#define XMIP_SUBSCRIPTIONS_ENTRYPOINT             "xmip_subscriptions_v1"
#define XMIP_SUBSCRIPTION_ACT_ENTRYPOINT          "xmip_subscription_act_v1"
#define XMIP_PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT "xmip_publication_subscriptions_v1"
#define XMIP_ORDER_ENTRYPOINT                     "xmip_order_v1"

/*
 * 15. A node's Dead Message Queue (ADR-0052, amendment 2026-10-01).
 *
 * An accepted Message that no Subscription matched is kept in the Ledger
 * with its entry in its node's Dead Message Queue: its receive context, what
 * its gates concluded, its promoted properties and every Subscription's
 * reason for declining, written with the Message in one write. It is not a
 * dead letter queue: a failed Journey never goes there. An Operator replays
 * one once a Subscription is added or fixed: its promoted properties are
 * routed against the node's Subscriptions of now, a Journey opened for each
 * match and held in its Subscription's queue, and the entry taken out, in
 * one write, once. A Message that still matches nothing stays.
 *
 * A list is JSON, written into out as UTF-8, its true byte length in out_len
 * whether or not it fit:
 *
 *   {"orders":"<where acts are left, empty for none>",
 *    "dead_messages":[{"node","message","sequence","location",
 *                      "received_unix_nanos","validation","promoted",
 *                      "declines"}]}
 *
 * node is the scope of the node whose queue keeps it; message the Message's
 * identifier, what a Replay names; sequence its place, oldest lowest;
 * location the Receive Location it arrived at; received_unix_nanos when;
 * validation, promoted and declines arrays of [name, value] pairs - each
 * gate and its verdict in the order they ran, each promoted property by
 * name, each Subscription and why it declined in the order asked. A node
 * lists its oldest hundred.
 *
 * xmip_dead_messages_v1 lists the Dead Message Queues of every node running
 * in this process at or beneath node (empty: every one); orders is empty.
 * XMIP_OK; XMIP_E_MALFORMED when node is not UTF-8.
 *
 * xmip_dead_message_replay_v1 replays the Message message from the Dead
 * Message Queue of the node at node in this process, by who. XMIP_OK with
 * what came of it in said, one sentence - also for a Message replayed
 * already, which is not replayed twice; XMIP_E_NOT_FOUND with the refusal,
 * opening REFUSED, when that node does not run here, its queue never kept
 * such a Message, or it still matches no Subscription; XMIP_E_IO, opening
 * FAILED, when Xmip Storage did not answer and nothing changed.
 *
 * xmip_publication_dead_messages_v1 lists what a read publication (section
 * 8) carries, orders as it says. XMIP_E_INVALID for no handle.
 *
 * A surface over a publication replays through section 14's xmip_order_v1,
 * noun dead-message, target the Message's identifier, act replay.
 *
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipDeadMessagesFn)(XmipScope node, uint8_t *out, size_t cap,
                                         size_t *out_len);
typedef XmipStatus (*XmipDeadMessageReplayFn)(XmipScope node, XmipStr message, XmipStr who,
                                              uint8_t *said, size_t said_cap,
                                              size_t *said_len);
typedef XmipStatus (*XmipPublicationDeadMessagesFn)(const XmipPublication *publication,
                                                    uint8_t *out, size_t cap,
                                                    size_t *out_len);

#define XMIP_DEAD_MESSAGES_ENTRYPOINT             "xmip_dead_messages_v1"
#define XMIP_DEAD_MESSAGE_REPLAY_ENTRYPOINT       "xmip_dead_message_replay_v1"
#define XMIP_PUBLICATION_DEAD_MESSAGES_ENTRYPOINT "xmip_publication_dead_messages_v1"

/*
 * 16. The Journeys that failed, listed; Retry and Dismiss (runtime-model.md
 * section 13; ADR-0013, amendment 2026-08-26).
 *
 * A Journey leads to one Send Port - a Send Port Group's Journeys are one per
 * Port - and when every Send Location of its Port failed its tries it is
 * written Failed, with why, and waits in its Port's queue for an operator.
 * A node's publication carries, at <node>/send/<Port>, what the Port sent,
 * what failed, how many failed wait in its queue, and the last Journey that
 * failed with why - Done while any waits, Fine once none does; and beside
 * its records, for every Port it sends, how many wait now (zero where none),
 * whether one blocks its sequence, and the oldest hundred with why: the
 * identifiers an act names; apart from them the last that failed there
 * since the node started, as history.
 * A Journey that failed before the node restarted, or on another node
 * sending the Port, is among them once the node's scan has read it.
 *
 * A list is JSON, written into out as UTF-8, its true byte length in out_len
 * whether or not it fit:
 *
 *   {"orders":"<where acts are left, empty for none>",
 *    "failed_journeys":[{"node","send_port","count","blocked","next",
 *                        "journeys":[{"journey","sequence","reason"}],
 *                        "last_failure":{"journey","reason"}|null}]}
 *
 * node is the scope of the node that sends the Port and send_port its
 * configured name; count how many failed Journeys the node knows wait in its
 * queue now, zero where none; blocked whether one of them blocks a
 * Sequential Port's sequence now; journeys those listed, oldest first -
 * journey the identifier an act names, sequence its place in the queue,
 * reason why it failed, in words; next the place the next page reads from,
 * or null where the queue was read to its end or the list is a
 * publication's; last_failure the last Journey that failed at the Port
 * since its node started, and why - history, which may since have been
 * retried or dismissed - or null where none has.
 *
 * xmip_failed_journeys_v1 lists, for every node running in this process at
 * or beneath node (empty: every one), the Journeys that failed at its Send
 * Port port (empty: every Port it sends), read from Xmip Storage from the
 * place from on (0: the oldest), at most most of each Port (0: a hundred);
 * a page reads at most 1024 entries of a queue, so a page may hold fewer
 * than most and still name a next. orders is empty. XMIP_OK; XMIP_E_MALFORMED
 * when node or port is not UTF-8; XMIP_E_IO with the reason, opening FAILED,
 * when Xmip Storage did not answer.
 *
 * xmip_publication_failed_journeys_v1 lists what a read publication (section
 * 8) carries, orders as it says, next null. XMIP_E_INVALID for no handle.
 *
 * xmip_journey_act_v1 applies act - retry or dismiss, exact - to the Journey
 * journey (its identifier) sent by the node at node in this process, by who.
 * Retry writes it Active, its tries begun anew, and moves it to the end of
 * its Send Port's queue, from where it is sent again; where it blocks a
 * Sequential Send Port it keeps its place. Dismiss writes it Dismissed -
 * terminal, its history, Message and Stream kept - and takes it out of the
 * queue, so a Sequential Port's next of its order key goes. Each is one
 * write under a claim, audited with who acted. XMIP_OK with what came of it
 * in said, one sentence - also for a Journey dismissed already; XMIP_E_NOT_FOUND
 * with the refusal, opening REFUSED, when that node does not run here, the
 * Ledger holds no such Journey, it has not failed, or that node does not
 * send its Send Port; XMIP_E_INVALID with the refusal for a word that is no
 * act on a Journey; XMIP_E_IO, opening FAILED, when Xmip Storage did not
 * answer or take it and nothing changed.
 *
 * A surface over a publication acts through section 14's xmip_order_v1,
 * noun journey, target the Journey's identifier, act retry or dismiss. Who
 * may act is the surface's to decide by role.
 *
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipJourneyActFn)(XmipScope node, XmipStr journey, XmipStr act,
                                       XmipStr who, uint8_t *said, size_t said_cap,
                                       size_t *said_len);

typedef XmipStatus (*XmipFailedJourneysFn)(XmipScope node, XmipStr port, uint64_t from,
                                           uint32_t most, uint8_t *out, size_t cap,
                                           size_t *out_len);
typedef XmipStatus (*XmipPublicationFailedJourneysFn)(const XmipPublication *publication,
                                                      uint8_t *out, size_t cap,
                                                      size_t *out_len);

#define XMIP_JOURNEY_ACT_ENTRYPOINT                 "xmip_journey_act_v1"
#define XMIP_FAILED_JOURNEYS_ENTRYPOINT             "xmip_failed_journeys_v1"
#define XMIP_PUBLICATION_FAILED_JOURNEYS_ENTRYPOINT "xmip_publication_failed_journeys_v1"

#ifdef __cplusplus
}
#endif

#endif /* XMIP_OPERATE_H */
