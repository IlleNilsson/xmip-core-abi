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
 * Receive Location, a Journey in an Xmip Process and a Message at a Send
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
 *     stage words, a parse  node::Stage           (xmip-core-node)
 *     pausable, location    node::Stage           (xmip-core-node)
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

/* The words a node may declare, in message-path order. Static. */
typedef XmipStatus (*XmipStageWordsFn)(XmipStr *out, size_t cap, size_t *out_len);

/*
 * The stages a declaration names - words separated by commas or +, each exact
 * lower case - in path order, each at most once, into stages (static words,
 * the fill shape of section 5). A declaration naming any other word is
 * XMIP_E_INVALID with no stage, and the refusal sentence is written into
 * refusal as UTF-8, its true byte length in refusal_len whether or not it fit
 * (ADR-0055: refused by name, never dropped). refusal_len is 0 on XMIP_OK.
 */
typedef XmipStatus (*XmipStageDeclaredFn)(XmipStr declared,
                                          XmipStr *stages, size_t cap, size_t *out_len,
                                          uint8_t *refusal, size_t refusal_cap,
                                          size_t *refusal_len);

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
 * published: its name in *out_node (borrowed from scope), its stages in the
 * fill shape (static words, path order), and *out_online 1 when it may
 * assume the internet. XMIP_E_NOT_FOUND when the record is not a capability
 * record at all; XMIP_E_INVALID, *out_node still written, when it names a
 * word that is no stage, the refusal written as xmip_stage_declared_v1
 * writes one.
 */
typedef XmipStatus (*XmipCapabilityPublishedFn)(XmipScope scope, XmipStr evidence,
                                                XmipStr *out_node,
                                                XmipStr *stages, size_t cap,
                                                size_t *out_len, uint8_t *out_online,
                                                uint8_t *refusal, size_t refusal_cap,
                                                size_t *refusal_len);

/*
 * One entry of a run's node list - edge-01=receive+send, or a bare name for a
 * node that declared no stage: the name in *out_node (borrowed from entry,
 * trimmed) and the stages in the fill shape. XMIP_E_INVALID, *out_node still
 * written, with the refusal, as above.
 */
typedef XmipStatus (*XmipCapabilityEntryFn)(XmipStr entry, XmipStr *out_node,
                                            XmipStr *stages, size_t cap, size_t *out_len,
                                            uint8_t *refusal, size_t refusal_cap,
                                            size_t *refusal_len);

#define XMIP_SCOPE_CONTAINS_ENTRYPOINT "xmip_scope_contains_v1"
#define XMIP_SCOPE_PARTS_ENTRYPOINT    "xmip_scope_parts_v1"
#define XMIP_SCOPE_NODE_ENTRYPOINT     "xmip_scope_node_v1"
#define XMIP_STAGE_WORDS_ENTRYPOINT    "xmip_stage_words_v1"
#define XMIP_STAGE_DECLARED_ENTRYPOINT "xmip_stage_declared_v1"
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
    XMIP_TOPOLOGY_ENDPOINT        = 14
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
    XMIP_RUN_TESTS        = 0,
    XMIP_RUN_NODES        = 1,
    XMIP_RUN_CAPABILITIES = 2,
    XMIP_RUN_ONLINE       = 3
} XmipRunList;

/*
 * Who published and at which scope, and the single values of the run and
 * the topology: has_run and has_topology are 1 when the publication says
 * either, and the fields beside them are empty when it does not.
 */
typedef struct {
    XmipStr   source;
    XmipScope node;
    uint8_t   has_run;
    XmipStr   cluster;
    XmipStr   stress;
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

#ifdef __cplusplus
}
#endif

#endif /* XMIP_OPERATE_H */
