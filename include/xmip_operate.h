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
 * observability-model.md section 6. Health is a mood, not a color: it names
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
    XMIP_RUN_ONLINE = 3,
    /* the nodes on machines of their own, never spawned; the rest simulated */
    XMIP_RUN_REAL   = 4
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
 * program names the program (Xmip.Gui.Web, xmip); directory is where its
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
 *    "fragments":[{"offset","length","sealed","said"}],
 *    "columns":["at",...],"severities":["information","warning","error"]}
 *
 * groups are one step down from where the query stands - clusters and hosts
 * at the top, a cluster's nodes and its own programs, a node's programs -
 * and none once a program is asked; chains are empty unless verify is yes,
 * and then each writer's chain - a node's location, or a program's name -
 * walked whole over every record of it, hidden ones among them, with the
 * first place it breaks (a record deleted, changed or out of order) or
 * that it is whole, in one sentence opening OK or FAILED; fragments are
 * empty unless verify is yes, and then each part of the file that is not a
 * whole record, apart from the chains: a record whose write was interrupted,
 * kept as written and sealed by the next append (sealed true, its sentence
 * opening FAILED), or the file's end no write has finished yet (sealed
 * false, opening UNFINISHED), each at its byte offset and length; columns
 * are the sort words in the order a reader shows them and severities least
 * first, so no surface keeps a list of its own. XMIP_OK with the answer;
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
 *                             For an editor (ADR-0064, amendment
 *                             2026-10-13): written, each table's section,
 *                             header keys, and the bytes of its header,
 *                             name, extent and every key and value;
 *                             references, each name standing for an entry
 *                             declared elsewhere, what it names, where that
 *                             is and every name it may take; keys, every
 *                             key each table may hold, its kind, words and
 *                             whether it is required, read from configure's
 *                             own types; and tuning, the runtime's
 *                             declaration of [tuning].
 *   xmip_filter_structure_v1  a filter's text in; its rows and groups out.
 *   xmip_filter_text_v1       rows and groups in; the filter's canonical
 *                             text out, which reads back byte for byte.
 *   xmip_cluster_edit_v1      a cluster's xmip.toml in, an edit as
 *                             argument; the edited text out, everything the
 *                             edit does not touch as it was. The edits: set,
 *                             remove, add-entry, remove-entry (refused while
 *                             anything names the entry, unless references),
 *                             add-node, rename (with every name standing
 *                             for it), refer, rename-key, and an
 *                             Application's own.
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
 * dismiss (section 16) - for the node to take and apply; said
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
 * dead letter queue: a failed Message never goes there. An Operator replays
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
 * 16. The Messages that failed, listed; Retry and Dismiss (runtime-model.md
 * section 13; ADR-0013, amendment 2026-08-26).
 *
 * A Message travels to one Send Port on each Journey - a Send Port Group's
 * Journeys are one per Port - and when every Send Location of its Port failed
 * its tries it is written Failed, with why, and waits in its Port's queue for an operator.
 * A node's publication carries, at <node>/send/<Port>, what the Port sent,
 * what failed, how many failed wait in its queue, and the last Message that
 * failed with why - Done while any waits, Fine once none does; and beside
 * its records, for every Port it sends, how many wait now (zero where none),
 * whether one blocks its sequence, and the oldest with why - the node's
 * [tuning] failed_window of them, a hundred by default: the identifiers an
 * act names; apart from them the last that failed there since the node
 * started, as history.
 * Xmip Storage keeps the count and every one of them in the queue's index
 * of failed Messages, written with the failure, so one that failed before
 * the node restarted, or on another node sending the Port, is among them.
 *
 * A list is JSON, written into out as UTF-8, its true byte length in out_len
 * whether or not it fit:
 *
 *   {"orders":"<where acts are left, empty for none>",
 *    "failed_messages":[{"node","send_port","count","blocked","next",
 *                        "messages":[{"journey","sequence","reason"}],
 *                        "last_failure":{"journey","reason"}|null}]}
 *
 * node is the scope of the node that sends the Port and send_port its
 * configured name; count how many failed Messages wait in its queue now,
 * as Xmip Storage counts them, zero where none; blocked whether one of them
 * blocks a Sequential Port's sequence now; messages those listed, oldest
 * first - journey its Journey's identifier, the one an act names, sequence its place in the
 * queue, reason why it failed, in words; next the place in the queue's
 * index of failed Messages the next page reads from, or null where the
 * index was read to its end or the list is a publication's; last_failure
 * the last Message that failed at the Port since its node started, and why -
 * history, which may since have been retried or dismissed - or null where none
 * has.
 *
 * xmip_failed_messages_v1 lists, for every node running in this process at
 * or beneath node (empty: every one), the Messages that failed at its Send
 * Port port (empty: every Port it sends), read from Xmip Storage from the
 * place from on in each Port's index of failed Messages (0: the oldest), at
 * most most of each Port (0: a hundred), each page one read of the index.
 * orders is empty. XMIP_OK; XMIP_E_MALFORMED
 * when node or port is not UTF-8; XMIP_E_IO with the reason, opening FAILED,
 * when Xmip Storage did not answer.
 *
 * xmip_publication_failed_messages_v1 lists what a read publication (section
 * 8) carries, orders as it says, next null. XMIP_E_INVALID for no handle.
 *
 * xmip_message_act_v1 applies act - retry or dismiss, exact - to the Message
 * whose Journey is journey (its identifier), sent by the node at node in this process, by who.
 * Retry writes it Active, its tries begun anew, and moves it to the end of
 * its Send Port's queue, from where it is sent again; where it blocks a
 * Sequential Send Port it keeps its place. Dismiss writes it Dismissed -
 * terminal, its history, Message and Stream kept - and takes it out of the
 * queue, so a Sequential Port's next of its order key goes. Each is one
 * write under a claim, audited with who acted. XMIP_OK with what came of it
 * in said, one sentence - also for a Message dismissed already; XMIP_E_NOT_FOUND
 * with the refusal, opening REFUSED, when that node does not run here, the
 * Ledger holds no such Journey, its Message has not failed, or that node does not
 * send its Send Port; XMIP_E_INVALID with the refusal for a word that is no
 * act on a Message; XMIP_E_IO, opening FAILED, when Xmip Storage did not
 * answer or take it and nothing changed.
 *
 * A surface over a publication acts through section 14's xmip_order_v1,
 * noun message, target the Journey's identifier, act retry or dismiss. Who
 * may act is the surface's to decide by role.
 *
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipMessageActFn)(XmipScope node, XmipStr journey, XmipStr act,
                                       XmipStr who, uint8_t *said, size_t said_cap,
                                       size_t *said_len);

typedef XmipStatus (*XmipFailedMessagesFn)(XmipScope node, XmipStr port, uint64_t from,
                                           uint32_t most, uint8_t *out, size_t cap,
                                           size_t *out_len);
typedef XmipStatus (*XmipPublicationFailedMessagesFn)(const XmipPublication *publication,
                                                      uint8_t *out, size_t cap,
                                                      size_t *out_len);

#define XMIP_MESSAGE_ACT_ENTRYPOINT                 "xmip_message_act_v1"
#define XMIP_FAILED_MESSAGES_ENTRYPOINT             "xmip_failed_messages_v1"
#define XMIP_PUBLICATION_FAILED_MESSAGES_ENTRYPOINT "xmip_publication_failed_messages_v1"

/*
 * 17. A node reached at its operate listener (ADR-0014, amendments
 * 2026-10-12 and 2026-10-13; ADR-0067; ADR-0052, amendment 2026-10-05 and
 * its note).
 *
 * The surface over a node's operate listener: a node on another machine,
 * reached over gRPC on TCP inside Xmip's mutual TLS (ADR-0063), or a node
 * on this one, reached over gRPC on its local endpoint - a named pipe on
 * Windows, a Unix domain socket elsewhere - without TLS, the operating
 * system admitting only the node's account and the machine's
 * administrators and proving who connected. Every surface operates a node
 * through these calls, on its machine and elsewhere alike, so the follow,
 * its recovery and every word are the runtime's, once. Running a command
 * on another machine stays shell remoting (ADR-0014 clause 6); SignalR
 * stays between a web host and the browser it serves.
 *
 * xmip_remote_open_v1 makes a handle over the node at address, host:port,
 * presenting certificate (a PEM chain, leaf first) with key (its PEM
 * private key) and taking the node only where its certificate names host
 * and reaches anchors (PEM). Nothing is connected yet. XMIP_OK; XMIP_E_INVALID
 * with the refusal in report for an address that is no operate address or
 * an identity that does not pair; XMIP_E_MALFORMED for text not UTF-8.
 *
 * xmip_remote_open_kept_v1 makes the same handle where the key never
 * leaves its platform's keeper - the Android Keystore, the iOS keychain's
 * Secure Enclave, Windows CNG and the TPM (ADR-0034, amendment
 * 2026-10-15): certificate is the PEM chain, leaf first, and in place of
 * the key, sign is called with context for every signature the handshake
 * needs - ECDSA on P-256 with SHA-256 over message, unhashed, the keeper
 * hashing it - writing the signature, DER, into out (room for 512 bytes)
 * and its length into out_len, and answering XMIP_OK; any other status
 * with the keeper's sentence in out refuses the handshake with it. sign
 * may be called from any thread, at once from several, until
 * xmip_remote_free_v1 returns: the binding keeps context alive until then.
 * Whether the chain's leaf is the keeper's key the handshake finds; the
 * node refuses a signature its certificate does not verify.
 *
 * xmip_remote_open_local_v1 makes the same handle over the node on this
 * machine whose local endpoint is called endpoint: a name - \\.\pipe\<name>
 * on Windows, /run/xmip/<name>.sock on Linux, /var/run/xmip/<name>.sock on
 * macOS - or that place itself. The node takes every call as the operating
 * system user this process runs as; no name crosses. Each call waits at
 * most within_ms; 0 takes what the node declared from its [tuning]
 * local_timeout, else 2 s, and a node that took longer is unreachable,
 * "the node did not answer within" the wait, until the follow hears it
 * again. XMIP_OK; XMIP_E_INVALID with the refusal in report for a name
 * that would leave the endpoints' folder. Every call below takes either
 * handle.
 *
 * xmip_remote_publication_v1 reads the node's publication - the text its
 * file holds, which section 8 reads - and its revision. XMIP_OK with the
 * text in out, its true length in out_len; XMIP_E_TRUNCATED where it did
 * not fit, the text kept and handed whole at the next call without asking
 * the node again; XMIP_E_UNAVAILABLE with why in out where the node did not
 * answer - said at once, without a second try, while the follow is
 * recovering it.
 *
 * xmip_remote_wait_v1 waits until the follow has told more than seen
 * times, or for timeout_ms, and says how many times it has told, the
 * revision of the node's publication, whether the node is reached, and
 * why not in reason (empty where it is). The follow starts at the first
 * wait and runs until the handle is freed, on a connection of its own:
 * the node answers its wait when the publication advances, so a surface
 * is told and never asks again and again. When the node is lost, or the
 * first try fails, every waiter is told at once with why, and the node is
 * tried again after 0, 2, 10 and 30 seconds, the last repeated; told once
 * more the moment it answers. XMIP_OK; XMIP_E_INVALID for no handle.
 *
 * xmip_remote_act_v1 asks the node to apply act to the noun called target
 * of the node at node - the nouns and acts of section 14's xmip_order_v1 -
 * as the identity the certificate proves, or on the local endpoint the
 * user the operating system says this process runs as; no name crosses. The node admits
 * it by the role it grants that identity (section 18) and audits it, taken
 * or refused. XMIP_OK taken; XMIP_E_AUTH refused by role; XMIP_E_NOT_FOUND
 * refused by the node, opening REFUSED; XMIP_E_INVALID no act the noun
 * takes; XMIP_E_IO, opening FAILED, Xmip Storage did not answer and nothing
 * changed; XMIP_E_UNAVAILABLE the node did not answer. The sentence in said.
 *
 * xmip_remote_end_v1 ends the follow and wakes every wait, from any
 * thread, while a call may still be in flight on another; a binding asks
 * it, lets every call in flight return, and then calls xmip_remote_free_v1,
 * which closes every connection and frees the handle.
 *
 * xmip_operate_serve_v1 serves, for a program hosting a node through this
 * boundary, what it posts and the acts on the nodes this process runs, on
 * an operate listener bound at listen, host:port, presenting certificate
 * with key and taking a surface only where its certificate reaches
 * anchors; every proven caller holds the role role names (section 18;
 * empty: Observer), and every act is audited as program. XMIP_OK; XMIP_E_IO
 * with why in report. xmip_operate_serve_local_v1 serves the same on this
 * machine's local endpoint called local, every caller the user its process
 * runs as; the plain text inside the machine is audited where it is made
 * (ADR-0063 clause 1). A node that xmip-service runs serves from its
 * configuration's [operate] instead.
 * xmip_operate_post_v1 posts publication - the text a publication file
 * holds (section 8) - as what the listener answers: a new revision where it
 * differs from the last, every surface following told at once. Until a
 * first post it answers revision 0 and an empty text.
 * xmip_operate_served_address_v1 says where it listens, host:port or the
 * local endpoint's path;
 * xmip_operate_serve_stop_v1 stops it - every connection shut, every wait
 * woken - and frees it.
 *
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef struct XmipRemote XmipRemote;
typedef struct XmipServed XmipServed;

typedef XmipStatus (*XmipRemoteOpenFn)(XmipStr address, XmipStr certificate, XmipStr key,
                                       XmipStr anchors, XmipRemote **out, uint8_t *report,
                                       size_t cap, size_t *out_len);
typedef XmipStatus (*XmipKeeperSignFn)(void *context, XmipSlice message, uint8_t *out,
                                       size_t cap, size_t *out_len);
typedef XmipStatus (*XmipRemoteOpenKeptFn)(XmipStr address, XmipStr certificate, XmipStr anchors,
                                           XmipKeeperSignFn sign, void *context,
                                           XmipRemote **out, uint8_t *report, size_t cap,
                                           size_t *out_len);
typedef XmipStatus (*XmipRemoteOpenLocalFn)(XmipStr endpoint, uint32_t within_ms,
                                            XmipRemote **out, uint8_t *report, size_t cap,
                                            size_t *out_len);
typedef XmipStatus (*XmipRemotePublicationFn)(const XmipRemote *remote, uint8_t *out,
                                              size_t cap, size_t *out_len,
                                              uint64_t *out_revision);
typedef XmipStatus (*XmipRemoteWaitFn)(const XmipRemote *remote, uint64_t seen,
                                       uint32_t timeout_ms, uint64_t *out_told,
                                       uint64_t *out_revision, uint8_t *out_reachable,
                                       uint8_t *reason, size_t cap, size_t *reason_len);
typedef XmipStatus (*XmipRemoteActFn)(const XmipRemote *remote, XmipScope node, XmipStr noun,
                                      XmipStr target, XmipStr act, uint8_t *said, size_t cap,
                                      size_t *said_len);
typedef void (*XmipRemoteEndFn)(const XmipRemote *remote);
typedef void (*XmipRemoteFreeFn)(XmipRemote *remote);
typedef XmipStatus (*XmipOperateServeFn)(XmipStr listen, XmipStr certificate, XmipStr key,
                                         XmipStr anchors, XmipStr role, XmipStr program,
                                         XmipServed **out, uint8_t *report, size_t cap,
                                         size_t *out_len);
typedef XmipStatus (*XmipOperateServeLocalFn)(XmipStr local, XmipStr role, XmipStr program,
                                              XmipServed **out, uint8_t *report, size_t cap,
                                              size_t *out_len);
typedef XmipStatus (*XmipOperateServedAddressFn)(const XmipServed *served, uint8_t *out,
                                                 size_t cap, size_t *out_len);
typedef XmipStatus (*XmipOperatePostFn)(const XmipServed *served, XmipStr publication);
typedef void (*XmipOperateServeStopFn)(XmipServed *served);

#define XMIP_REMOTE_OPEN_ENTRYPOINT            "xmip_remote_open_v1"
#define XMIP_REMOTE_OPEN_KEPT_ENTRYPOINT       "xmip_remote_open_kept_v1"
#define XMIP_REMOTE_OPEN_LOCAL_ENTRYPOINT      "xmip_remote_open_local_v1"
#define XMIP_REMOTE_PUBLICATION_ENTRYPOINT     "xmip_remote_publication_v1"
#define XMIP_REMOTE_WAIT_ENTRYPOINT            "xmip_remote_wait_v1"
#define XMIP_REMOTE_ACT_ENTRYPOINT             "xmip_remote_act_v1"
#define XMIP_REMOTE_END_ENTRYPOINT             "xmip_remote_end_v1"
#define XMIP_REMOTE_FREE_ENTRYPOINT            "xmip_remote_free_v1"
#define XMIP_OPERATE_SERVE_ENTRYPOINT          "xmip_operate_serve_v1"
#define XMIP_OPERATE_SERVE_LOCAL_ENTRYPOINT    "xmip_operate_serve_local_v1"
#define XMIP_OPERATE_SERVED_ADDRESS_ENTRYPOINT "xmip_operate_served_address_v1"
#define XMIP_OPERATE_POST_ENTRYPOINT           "xmip_operate_post_v1"
#define XMIP_OPERATE_SERVE_STOP_ENTRYPOINT     "xmip_operate_serve_stop_v1"

/*
 * 18. The role a caller holds, and the one check every act passes
 * (ADR-0009, amendments 2026-10-03 and 2026-10-06).
 *
 * A role crosses as an int, least first: 0 Observer, 1 Operator, 2
 * Developer - a person's security role, not a node's roles of section 7.
 * xmip_security_role_named_v1 names the role a word names, without regard
 * to case, Observer for any other word, so a misconfiguration grants
 * nothing. xmip_security_role_words_v1 writes what a role is called and
 * what it may do in the words a host's badge uses, both static;
 * XMIP_E_INVALID for no role. xmip_act_admitted_v1 is the gate a node's
 * operate listener and every surface's acts pass: whether who - proven
 * where proven is non-zero, the identity a connection proved and never a
 * name a caller gave - holding role may act on target. XMIP_OK with nothing
 * said; XMIP_E_AUTH with the refusal in said, opening REFUSED.
 *
 * Pure, as section 7: no handle, any thread. Optional symbols;
 * XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipSecurityRoleNamedFn)(XmipStr word, int32_t *out);
typedef XmipStatus (*XmipSecurityRoleWordsFn)(int32_t role, XmipStr *out_word,
                                              XmipStr *out_description);
typedef XmipStatus (*XmipActAdmittedFn)(XmipStr who, uint8_t proven, int32_t role, XmipStr act,
                                        XmipStr target, uint8_t *said, size_t cap,
                                        size_t *said_len);

#define XMIP_SECURITY_ROLE_NAMED_ENTRYPOINT "xmip_security_role_named_v1"
#define XMIP_SECURITY_ROLE_WORDS_ENTRYPOINT "xmip_security_role_words_v1"
#define XMIP_ACT_ADMITTED_ENTRYPOINT "xmip_act_admitted_v1"

/* ===================================================================== */
/* 19. A person's certificate: requested, and signed                     */
/* ===================================================================== */

/*
 * An operator's certificate (ADR-0034, amendment 2026-10-15): client
 * authentication alone, its subject the person - the caller a node's
 * operate listener proves (section 18; ADR-0009, note of 2026-10-12). The
 * surface makes its own key and a request (PKCS #10) naming the person; the
 * cluster's authority signs it once it is approved out of band. Both rules
 * are xmip-core-provision's; these forward.
 *
 * xmip_operator_request_v1 makes a fresh P-256 key and the request for
 * person - a person's name as their directory knows it, a user principal
 * name where there is one, one to 64 characters, no control character and
 * none RFC 4514 escapes - and writes them into folder (a path, created
 * where absent) as operator.key (PRIVATE KEY PEM) and operator.csr
 * (CERTIFICATE REQUEST PEM). Each is a private file: readable by the user
 * the process runs as alone - mode 0600 in a 0700 folder on Unix, an
 * access list of that user and SYSTEM on Windows - set before its first
 * byte, synced and moved into place whole. The key never crosses this
 * boundary. A file already there is never overwritten: the call is refused
 * and nothing is written. The answer is JSON, {"key","request"}, the two
 * files' paths. Every build carries it, the phone's remote build too.
 *
 * Where the key never leaves its platform's keeper - the Android Keystore,
 * the iOS keychain's Secure Enclave, Windows CNG and the TPM - the request
 * is made in two halves around the keeper's signature, so the surface
 * never writes DER: xmip_operator_information_v1 answers what the request
 * signs for person over public_key, the keeper's P-256 public key in the
 * form its platform hands it - SubjectPublicKeyInfo DER, or the bare
 * uncompressed point (X9.63, 65 bytes) - as DER bytes; the keeper signs
 * them, ECDSA with SHA-256, DER; and xmip_operator_assemble_v1 makes the
 * request (PEM, CERTIFICATE REQUEST) of information and signature,
 * verifying the signature under the key the information holds first, so a
 * keeper that signed in another form is told so here. Every build carries
 * both, the phone's remote build too.
 *
 * xmip_operator_keep_v1 keeps text - the chain the authority signed, or
 * the anchors a surface trusts, PEM, not secret - whole at the file path:
 * written beside it and synced, then, where replace is non-zero, renamed
 * over what was there, so a reader finds the old file or the new one and
 * never half of either; where replace is zero, linked where nothing is and
 * never over a file already there - refused, and nothing written - for a
 * chain written to a file a person named. The folder is synced after on
 * Unix. What the text must be is the surface's to check first. The answer
 * is empty. Every build carries it, the phone's remote build too.
 *
 * xmip_operator_sign_v1 signs the request text (PEM) by the authority the
 * node configuration at configuration (a path) names in
 * [provision.operator]: authority, the folder the cluster's authority keeps
 * authority.key and authority.pem in, relative to that file; lifetime, how
 * long the certificate is valid. The answer is the chain, PEM, leaf first.
 * The request's signature is verified under its own key, and a request
 * asking more than an operator's certificate carries - another usage, a
 * host's name, an authority's constraint, any other extension - is refused.
 * A node's build only, as sections 10 to 16.
 *
 * Each writes its answer into out - UTF-8, the information's DER bytes -
 * its true byte length in out_len whether or not it fit. XMIP_OK with the
 * answer; XMIP_E_INVALID with the refusal, one sentence; XMIP_E_MALFORMED
 * when a string is not UTF-8. Pure, as section 7: no handle, any thread.
 * Optional symbols; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipOperatorRequestFn)(XmipStr person, XmipStr folder, uint8_t *out,
                                            size_t cap, size_t *out_len);
typedef XmipStatus (*XmipOperatorInformationFn)(XmipStr person, XmipSlice public_key,
                                                uint8_t *out, size_t cap, size_t *out_len);
typedef XmipStatus (*XmipOperatorAssembleFn)(XmipSlice information, XmipSlice signature,
                                             uint8_t *out, size_t cap, size_t *out_len);
typedef XmipStatus (*XmipOperatorKeepFn)(XmipStr path, XmipStr text, uint8_t replace,
                                         uint8_t *out, size_t cap, size_t *out_len);
typedef XmipStatus (*XmipOperatorSignFn)(XmipStr configuration, XmipStr request,
                                         uint8_t *out, size_t cap, size_t *out_len);

#define XMIP_OPERATOR_REQUEST_ENTRYPOINT "xmip_operator_request_v1"
#define XMIP_OPERATOR_INFORMATION_ENTRYPOINT "xmip_operator_information_v1"
#define XMIP_OPERATOR_ASSEMBLE_ENTRYPOINT "xmip_operator_assemble_v1"
#define XMIP_OPERATOR_KEEP_ENTRYPOINT "xmip_operator_keep_v1"
#define XMIP_OPERATOR_SIGN_ENTRYPOINT "xmip_operator_sign_v1"

/* ===================================================================== */
/* 20. A database's schema: its upgrade between two releases             */
/* ===================================================================== */

/*
 * Every Xmip database records the schema version it holds (open problem 41;
 * ADR-0015, amendment 2026-10-20), and a node publishes each database it
 * keeps at <node>/storage/<domain> - runtime, administration, audit - its
 * evidence "schema version <n> on <engine>" - and at <node>/storage which
 * Storage it uses and whether it answers. A release's schema only adds
 * to the one before; its upgrade is the difference between the two, and
 * the schema, its versions and the scripts are xmip-core-persist's.
 *
 * xmip_database_upgrade_v1 writes the scripts that bring each database on
 * server - postgresql, mssql or mariadb - from schema version from to to,
 * zero for this build's: JSON, {"version", "scripts": [{"name", "text"}]},
 * version this build's schema version, and one script to each database,
 * named 04-<domain>-upgrade-<from>-<to>.sql, for IT to run as the schema's
 * owner. XMIP_OK with the answer; XMIP_E_INVALID with the refusal, one
 * sentence - a server Xmip Storage keeps no database on, from not a version
 * before to, to past this build's; XMIP_E_MALFORMED when server is not
 * UTF-8. The answer goes into out, its true byte length in out_len whether
 * or not it fit. Pure, as section 7: no handle, any thread. A node's build
 * only, as sections 10 to 16. Optional symbol; XMIP_OPERATE_VERSION is
 * unchanged.
 */
typedef XmipStatus (*XmipDatabaseUpgradeFn)(XmipStr server, uint32_t from, uint32_t to,
                                            uint8_t *out, size_t cap, size_t *out_len);

#define XMIP_DATABASE_UPGRADE_ENTRYPOINT "xmip_database_upgrade_v1"

/* ===================================================================== */
/* 21. A web host's secure surface, protected by Xmip itself             */
/* ===================================================================== */

/*
 * A web host - the web GUI, a .NET web server - hands the protection of
 * its connections to the runtime and keeps HTTP, so a browser is served by
 * Xmip's own stack and never the platform's (ADR-0009, amendment
 * 2026-10-21; the owner, 2026-09-26: Xmip uses its own tool chain). The
 * protection is a parameter, never a name (the owner, 2026-10-21:
 * technology and security should not be in functions, parameters is ok):
 * XMIP_PROTECTION_TLS, xmip-core-library-tls, is the one built, and any
 * other is answered XMIP_E_UNSUPPORTED with why in report. The host presents its
 * certificate and asks every browser for one: one that reaches the anchors
 * names the browser's caller, one that does not ends the handshake here,
 * and a browser that presents none is let through as no one, who only
 * watches. HTTP/1.1 is the one protocol agreed (ALPN, RFC 7301).
 *
 * xmip_host_issue_v1 keeps the authority in the folder authority -
 * authority.key and authority.pem, private files, made there and called
 * name where none is kept, as the cluster's own authority is - and issues
 * host a fresh key and a certificate under it for server authentication
 * alone, its subject and only alternative name host: an IP address as an
 * iPAddress (RFC 5280), which a browser checks https://127.0.0.1 against,
 * any other name as a dNSName. Written into folder (created where absent)
 * as host.key and host.pem, the chain leaf first, private files in place of
 * what was there: issued anew at each call. The answer is JSON,
 * {"certificate","key","anchor"}, the three files' paths, the anchor the
 * authority's certificate. XMIP_E_INVALID with the refusal. The same
 * authority signs a tester's operator request (section 19, a configuration
 * whose [provision.operator] authority names the folder).
 *
 * xmip_listener_open_v1 listens on listen, host:port, protected by
 * protection, presenting certificate (a PEM chain, leaf first) with key
 * (its PEM private key) and verifying a browser's certificate against
 * anchors (PEM): XMIP_OK with the handle in out; XMIP_E_IO with why in
 * report - an identity that does not read or pair, anchors holding no
 * certificate, an address not bound. xmip_listener_address_v1 says where
 * it listens, host:port, the port a bind to port 0 was given included.
 *
 * xmip_listener_accept_v1 waits for the next connection whose handshake has
 * finished and hands it over as a session in out. Each handshake runs on a
 * thread of its own, bounded at ten seconds, so a browser that connects and
 * says nothing holds up no other. XMIP_OK with the session; XMIP_E_AUTH
 * with why in report for a browser whose certificate the anchors refused -
 * a host tells its audit; XMIP_E_IO for one that did not finish for any
 * other reason - it stopped, spoke no TLS, ran out of time. Neither ends
 * the listener: the host accepts again. XMIP_E_CANCELLED once the listener
 * is stopped. xmip_listener_stop_v1 stops it from any thread, while an accept
 * may be in flight on another, waking every accept; xmip_listener_free_v1
 * frees it once every call on it has returned, stopping it where it was
 * not. A session already handed over stays its holder's.
 *
 * A session is read on one thread and written on another at once, as a
 * web server pumps a connection. xmip_session_peer_v1 writes the leaf
 * certificate its browser presented, DER, empty where it presented none;
 * xmip_session_ends_v1 where it was accepted and where its browser is,
 * host:port each, one space between. xmip_session_read_v1 waits for what the
 * browser sent next, at most cap bytes into out, their count in out_len,
 * 0 once the connection has ended - closed, cleanly or not, or reset;
 * XMIP_E_IO where a record could not be read. xmip_session_write_v1 writes
 * bytes whole and sends them; XMIP_E_IO where the connection has ended.
 * xmip_session_close_v1 closes it from any thread - the browser told where
 * no write holds the connection, the socket shut both ways - waking a read
 * waiting on another; xmip_session_free_v1 frees it once every call
 * on it has returned.
 *
 * Answers that do not fit are cut at cap, their true length in out_len,
 * except a read's, which reads at most cap. XMIP_E_INVALID for no handle;
 * XMIP_E_MALFORMED when a string is not UTF-8. A node's build only, as
 * sections 10 to 16. Optional symbols; XMIP_OPERATE_VERSION is unchanged.
 */
#define XMIP_PROTECTION_TLS 1

typedef struct XmipListener XmipListener;
typedef struct XmipSession XmipSession;

typedef XmipStatus (*XmipHostIssueFn)(XmipStr authority, XmipStr name, XmipStr host,
                                      XmipStr folder, uint8_t *out, size_t cap,
                                      size_t *out_len);
typedef XmipStatus (*XmipListenerOpenFn)(int32_t protection, XmipStr listen,
                                         XmipStr certificate, XmipStr key, XmipStr anchors,
                                         XmipListener **out, uint8_t *report, size_t cap,
                                         size_t *out_len);
typedef XmipStatus (*XmipListenerAddressFn)(const XmipListener *listener, uint8_t *out,
                                            size_t cap, size_t *out_len);
typedef XmipStatus (*XmipListenerAcceptFn)(const XmipListener *listener, XmipSession **out,
                                           uint8_t *report, size_t cap, size_t *report_len);
typedef void (*XmipListenerStopFn)(const XmipListener *listener);
typedef void (*XmipListenerFreeFn)(XmipListener *listener);
typedef XmipStatus (*XmipSessionPeerFn)(const XmipSession *session, uint8_t *out,
                                        size_t cap, size_t *out_len);
typedef XmipStatus (*XmipSessionEndsFn)(const XmipSession *session, uint8_t *out,
                                        size_t cap, size_t *out_len);
typedef XmipStatus (*XmipSessionReadFn)(const XmipSession *session, uint8_t *out,
                                        size_t cap, size_t *out_len);
typedef XmipStatus (*XmipSessionWriteFn)(const XmipSession *session, XmipSlice bytes);
typedef void (*XmipSessionCloseFn)(const XmipSession *session);
typedef void (*XmipSessionFreeFn)(XmipSession *session);

#define XMIP_HOST_ISSUE_ENTRYPOINT       "xmip_host_issue_v1"
#define XMIP_LISTENER_OPEN_ENTRYPOINT    "xmip_listener_open_v1"
#define XMIP_LISTENER_ADDRESS_ENTRYPOINT "xmip_listener_address_v1"
#define XMIP_LISTENER_ACCEPT_ENTRYPOINT  "xmip_listener_accept_v1"
#define XMIP_LISTENER_STOP_ENTRYPOINT    "xmip_listener_stop_v1"
#define XMIP_LISTENER_FREE_ENTRYPOINT    "xmip_listener_free_v1"
#define XMIP_SESSION_PEER_ENTRYPOINT     "xmip_session_peer_v1"
#define XMIP_SESSION_ENDS_ENTRYPOINT     "xmip_session_ends_v1"
#define XMIP_SESSION_READ_ENTRYPOINT     "xmip_session_read_v1"
#define XMIP_SESSION_WRITE_ENTRYPOINT    "xmip_session_write_v1"
#define XMIP_SESSION_CLOSE_ENTRYPOINT    "xmip_session_close_v1"
#define XMIP_SESSION_FREE_ENTRYPOINT     "xmip_session_free_v1"

/* ===================================================================== */
/* 22. A node's certificate, issued by the cluster's own authority       */
/* ===================================================================== */

/*
 * A node put on a machine of its own by an environment template is given
 * its certificate from the cluster's own authority, Xmip's internal CA and
 * never the platform's, issued on the operator's machine before the node
 * is installed (ADR-0015, amendment 2026-10-21), since a node starts no
 * provisioner of its own yet.
 *
 * xmip_node_issue_v1 keeps the authority in the folder authority -
 * authority.key and authority.pem, private files, made there and called
 * name where none is kept, as section 21's - and issues node a fresh key
 * and a certificate under it for server and client authentication both,
 * which mutual TLS between nodes needs, its subject node and its names
 * node and every one of names, comma separated: what the node is reached
 * by, its own name and its cluster's, as each resolves. Written into folder
 * (created where absent) as node.pem, the chain leaf first, node.key and
 * anchors.pem, the authority's certificate, private files in place of what
 * was there: the folder is what a node's [operate] certificate, key and
 * anchors name, shipped whole. The answer is JSON,
 * {"certificate","key","anchors"}, the three files' paths. XMIP_E_INVALID
 * with the refusal; XMIP_E_MALFORMED when a string is not UTF-8. Pure, as
 * section 7. A node's build only, as section 19's signing. Optional
 * symbol; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipNodeIssueFn)(XmipStr authority, XmipStr name, XmipStr node,
                                      XmipStr names, XmipStr folder, uint8_t *out,
                                      size_t cap, size_t *out_len);

#define XMIP_NODE_ISSUE_ENTRYPOINT "xmip_node_issue_v1"

/* ===================================================================== */
/* 23. The cluster's authority, and certificates across an air gap       */
/* ===================================================================== */

/*
 * The cluster's own authority kept in a folder, and a node's certificate
 * carried to it and back where no network route reaches it (ADR-0034,
 * amendment 2026-10-22). The authority keeps a register beside its key:
 * every certificate it signs - by ACME, by section 22, by a carried
 * request, a person's by section 19 - each revocation, and the revocation
 * list it publishes at the plain-HTTP address its leaves name.
 *
 * xmip_authority_v1 does the act act on arguments, a JSON object, and
 * answers JSON. The acts, and the members each reads:
 *   set-up   authority, name, publish (optional, http://host:port): the
 *            authority kept in authority, made there called name where
 *            none is, and the address it publishes at; answered as inspect.
 *   inspect  authority: the authority, its list, and every certificate it
 *            signed with its standing - current, renewal due, expired or
 *            revoked.
 *   read     certificate: a chain file's leaf and its standing.
 *   revoke   authority, serial (hexadecimal), reason (an RFC 5280 word,
 *            unspecified unless said): revoked and listed at once.
 *   export   authority, path, format (pem, der or crl): the authority's
 *            certificate or its list written to path, never over a file.
 *   back-up  authority, folder: the whole authority copied into an empty
 *            folder, each file private; the copy is the same authority.
 *   request  node, names (an array), folder: a fresh key, node.next.key,
 *            and a request for it, node.csr, written on the node.
 *   sign     authority, request (a file), names (an array; the request
 *            must ask for exactly these where any are given), out
 *            (optional, a file, never over one): the request verified and
 *            signed for ninety days, server and client authentication.
 *   import   certificate (a file), folder: the chain checked - every
 *            signature, the leaf for node.next.key and the request's
 *            names, valid now, reaching anchors.pem where the folder holds
 *            one - and written as node.pem, node.key and anchors.pem.
 * Every act is the capability's; nothing is decided here. XMIP_E_INVALID
 * with the refusal in words; XMIP_E_MALFORMED when a string is not UTF-8.
 * Pure, as section 7. A node's build only, as section 22. Optional symbol;
 * XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipAuthorityFn)(XmipStr act, XmipStr arguments, uint8_t *out,
                                      size_t cap, size_t *out_len);

#define XMIP_AUTHORITY_ENTRYPOINT "xmip_authority_v1"

/* ===================================================================== */
/* 24. A secret a Location presents, set through a node                  */
/* ===================================================================== */

/*
 * A Location that connects to a party - SFTP, FTP and FTPS, HTTP and
 * HTTPS, a database - names the proof it presents with secret = "<name>"
 * (ADR-0019, amendment 2026-10-29). The secret is never in the TOML: it is
 * kept in the Xmip Administration Database, in its own table, its value
 * sealed by Xmip whatever store keeps it, and read by the node as the Location
 * next connects. Its kinds: password, key (an SSH private key, PEM,
 * OpenSSH's form, unencrypted) and certificate (a PEM chain, leaf first,
 * with its private key).
 *
 * xmip_secret_v1 sets the secret name of kind to value through the node at
 * node, over a handle of section 17, as the identity the handle's
 * connection proves. The value crosses inside the operate listener's TLS,
 * or on the local endpoint the operating system guards, and is never said
 * back. The node admits the act by the role it grants that identity
 * (section 18) and audits it with the name, never the value. A secret of
 * the same name is replaced and keeps its identifier. XMIP_OK set;
 * XMIP_E_AUTH refused by role; XMIP_E_NOT_FOUND refused by the node -
 * no such node, a name or a kind that is none, a value not of its kind -
 * opening REFUSED; XMIP_E_IO, opening FAILED, Xmip Storage did not take
 * it; XMIP_E_UNAVAILABLE the node did not answer; XMIP_E_MALFORMED a
 * string not UTF-8. The sentence in said.
 *
 * xmip_secrets_v1 lists the secrets the node at node keeps, as JSON in
 * out, its true length in out_len whether or not it fit:
 *
 *   {"secrets":[{"name","kind","set_unix_nanos","set_by"}]}
 *
 * never a value, to an Operator and up. XMIP_OK; XMIP_E_TRUNCATED where it
 * did not fit; XMIP_E_AUTH refused by role; XMIP_E_NOT_FOUND no such node, XMIP_E_IO Xmip Storage did not answer,
 * XMIP_E_UNAVAILABLE the node did not answer, each with why in out.
 *
 * Optional symbols, as section 7's are; XMIP_OPERATE_VERSION is unchanged.
 */
typedef XmipStatus (*XmipSecretFn)(const XmipRemote *remote, XmipScope node, XmipStr name,
                                   XmipStr kind, XmipStr value, uint8_t *said, size_t cap,
                                   size_t *said_len);
typedef XmipStatus (*XmipSecretsFn)(const XmipRemote *remote, XmipScope node, uint8_t *out,
                                    size_t cap, size_t *out_len);

#define XMIP_SECRET_ENTRYPOINT  "xmip_secret_v1"
#define XMIP_SECRETS_ENTRYPOINT "xmip_secrets_v1"
#ifdef __cplusplus
}
#endif

#endif /* XMIP_OPERATE_H */
