/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_module.h - the Xmip module boundary, ABI version 1.
 *
 * A module is a shared library that exports exactly one symbol and answers
 * through function tables. This header is the normative boundary described by
 * ADR-0012. Nothing about Rust appears here, and nothing about Rust may appear
 * here. A module written in C, C++, Zig, Go or Rust is the same module to the
 * host, and the host cannot tell which it loaded.
 *
 * Licence. AGPL-3.0-or-later, like the rest of Xmip. There is no exception for
 * the boundary. A user takes Xmip under Xmip's licence, and may additionally have
 * to satisfy the licences of what Xmip itself depends on. Reconciling that is the
 * user's business, not Xmip's.
 *
 * The boundary is still a boundary in the sense that matters here: it is a C ABI,
 * it is language-neutral, and no implementer is obliged to write Rust or link
 * Xmip code. It is not a licence exemption.
 */

#ifndef XMIP_MODULE_H
#define XMIP_MODULE_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* ===================================================================== */
/* 1. Version and entrypoint                                             */
/* ===================================================================== */

#define XMIP_ABI_VERSION  1u
#define XMIP_ENTRYPOINT   "xmip_create_module_v1"

/*
 * Exporting the entrypoint.
 *
 * The symbol must have C linkage and be visible outside the shared library.
 * How that is spelled differs by platform, so the macro spells it:
 *
 *     XMIP_EXPORT XmipStatus
 *     xmip_create_module_v1(const XmipHost *host, XmipModule *out);
 *
 * On Windows nothing is exported unless it is asked for. On ELF and Mach-O
 * everything is exported unless the build hides it, and a module built with
 * -fvisibility=hidden - which it should be - needs the attribute back.
 *
 * A Rust module writes #[no_mangle] pub extern "C" and needs no macro.
 */
#if defined(_WIN32) || defined(__CYGWIN__)
#  define XMIP_EXPORT __declspec(dllexport)
#elif defined(__GNUC__) || defined(__clang__)
#  define XMIP_EXPORT __attribute__((visibility("default")))
#else
#  define XMIP_EXPORT
#endif

/*
 * The file the host looks for. Naming is a platform convention, not an Xmip
 * decision, and the host applies it rather than the module author:
 *
 *     linux      libxmip_core_transport_http.so
 *     macos      libxmip_core_transport_http.dylib
 *     windows    xmip_core_transport_http.dll
 *
 * The repository name with hyphens replaced by underscores, the platform
 * prefix where the platform has one, the platform suffix always.
 */
#if defined(_WIN32)
#  define XMIP_MODULE_PREFIX ""
#  define XMIP_MODULE_SUFFIX ".dll"
#elif defined(__APPLE__)
#  define XMIP_MODULE_PREFIX "lib"
#  define XMIP_MODULE_SUFFIX ".dylib"
#else
#  define XMIP_MODULE_PREFIX "lib"
#  define XMIP_MODULE_SUFFIX ".so"
#endif

/* ===================================================================== */
/* 2. Primitives                                                         */
/* ===================================================================== */

/*
 * A borrowed byte range. Never owned by the receiver, never null-terminated.
 * Valid only for the duration of the call it was passed to, unless the
 * function documents otherwise. `ptr` may be NULL only when `len` is 0.
 */
typedef struct {
    const uint8_t *ptr;
    size_t         len;
} XmipSlice;

/*
 * A borrowed UTF-8 string. Not null-terminated. The producer guarantees valid
 * UTF-8; a receiver that checks and finds otherwise returns XMIP_E_MALFORMED.
 */
typedef XmipSlice XmipStr;

/*
 * An owned byte buffer. Released by calling the release function carried in
 * the buffer itself - never with free(), never with the receiver's allocator.
 * No allocator is shared across this boundary.
 */
typedef struct {
    uint8_t *ptr;
    size_t   len;
    void    *owner;
    void   (*release)(void *owner, uint8_t *ptr, size_t len);
} XmipBuffer;

#define XMIP_BUFFER_RELEASE(b) \
    do { if ((b).release) (b).release((b).owner, (b).ptr, (b).len); } while (0)

/* ===================================================================== */
/* 3. Status                                                             */
/* ===================================================================== */

typedef int32_t XmipStatus;

#define XMIP_OK                 0

/* Caller error. The call was wrong; repeating it unchanged will fail again. */
#define XMIP_E_INVALID        (-1)   /* argument outside its contract        */
#define XMIP_E_UNSUPPORTED    (-2)   /* well formed, not implemented here    */
#define XMIP_E_STATE          (-3)   /* wrong lifecycle state for this call  */
#define XMIP_E_NOT_FOUND      (-4)

/* Data. The input is at fault, not the caller and not the environment. */
#define XMIP_E_MALFORMED     (-10)   /* not the standard it claims to be     */
#define XMIP_E_CONTRACT      (-11)   /* well formed, violates the contract   */
#define XMIP_E_TRUNCATED     (-12)   /* stream ended mid-structure           */

/* Environment. */
#define XMIP_E_IO            (-20)
#define XMIP_E_TIMEOUT       (-21)
#define XMIP_E_UNAVAILABLE   (-22)   /* peer refused or is down              */
#define XMIP_E_AUTH          (-23)
#define XMIP_E_CAPACITY      (-24)   /* quota, limit or resource exhaustion  */

/* Control. */
#define XMIP_E_CANCELLED     (-30)   /* host asked for cancellation          */
#define XMIP_E_AGAIN         (-31)   /* would block; not a failure           */

/* Terminal. The module instance is unusable and must be destroyed. */
#define XMIP_E_INTERNAL      (-40)   /* module bug                           */
#define XMIP_E_PANIC         (-41)   /* unwinding was caught at the boundary */

/*
 * Retryability is a property of the code, not of the call site, so that
 * xmip-core-resilience can decide without knowing the module.
 * Retryable: TIMEOUT, UNAVAILABLE, CAPACITY, AGAIN. Nothing else.
 */
#define XMIP_IS_RETRYABLE(s) \
    ((s) == XMIP_E_TIMEOUT || (s) == XMIP_E_UNAVAILABLE || \
     (s) == XMIP_E_CAPACITY || (s) == XMIP_E_AGAIN)

#define XMIP_IS_TERMINAL(s) \
    ((s) == XMIP_E_INTERNAL || (s) == XMIP_E_PANIC)

/*
 * A structured diagnostic. Used where a status code alone loses information
 * that an operator needs - contract validation above all. Borrowed: valid
 * until the next call on the same module instance.
 */
typedef struct {
    XmipStatus code;
    XmipStr    message;    /* human readable, one line, no trailing newline  */
    XmipStr    location;   /* a path expression, or empty                    */
    uint64_t   offset;     /* byte offset in the stream, or UINT64_MAX       */
} XmipDiagnostic;

/* ===================================================================== */
/* 4. Descriptor                                                         */
/* ===================================================================== */

/*
 * What the module says it is. The three name parts are the same three parts
 * as the repository name under ADR-0011, so the descriptor of
 * xmip-saxon-transform-xslt reads provider="saxon", module="transform",
 * standard="xslt". The host rejects a module whose descriptor disagrees with
 * the artifact that asked for it.
 *
 * trait_* is the trait version the module was built against. module_* is the
 * module's own version and carries no compatibility meaning for the host.
 */
typedef struct {
    uint32_t abi_version;
    XmipStr  provider;
    XmipStr  module;
    XmipStr  standard;      /* empty only when provider is "core"            */
    uint32_t trait_major;
    uint32_t trait_minor;
    uint32_t module_major;
    uint32_t module_minor;
    uint32_t module_patch;
} XmipModuleDescriptor;

/* ===================================================================== */
/* 5. Streams                                                            */
/* ===================================================================== */

/*
 * Xmip streams may be larger than memory, so a stream never crosses the
 * boundary as a buffer. It crosses as a pair of function pointers.
 */

/*
 * Pull source. Returns bytes written into buf, 0 at end of stream, or a
 * negative XmipStatus. A short read is not end of stream.
 */
typedef struct {
    void   *ctx;
    int64_t (*read)(void *ctx, uint8_t *buf, size_t len);
} XmipReader;

/*
 * Push sink. write returns bytes accepted or a negative XmipStatus; a partial
 * write is legal and the caller re-offers the remainder. finish is called
 * exactly once, including on the failure path, with the outcome so far - so
 * a sink can distinguish a completed stream from an abandoned one.
 */
typedef struct {
    void      *ctx;
    int64_t    (*write)(void *ctx, const uint8_t *buf, size_t len);
    XmipStatus (*finish)(void *ctx, XmipStatus outcome);
} XmipWriter;

/* ===================================================================== */
/* 6. Host                                                               */
/* ===================================================================== */

typedef enum {
    XMIP_LOG_ERROR = 1,
    XMIP_LOG_WARN  = 2,
    XMIP_LOG_INFO  = 3,
    XMIP_LOG_DEBUG = 4,
    XMIP_LOG_TRACE = 5
} XmipLogLevel;

/*
 * What a module may call back into. Deliberately small. A module does not get
 * an allocator, a thread pool, a clock or a configuration store from the host;
 * it brings its own. What it cannot bring is the host's identity for a
 * journey, and the host's answer on cancellation.
 */
typedef struct {
    uint32_t abi_version;
    void    *ctx;

    void   (*log)(void *ctx, int32_t level, XmipStr target, XmipStr message);

    /* Cooperative cancellation. Non-zero means unwind and return
     * XMIP_E_CANCELLED. Cheap enough to call in a read loop. */
    int32_t (*cancelled)(void *ctx);

    /* Correlation for whatever call is in flight, for logs and audit.
     * Empty when the module is running outside a journey. */
    XmipStr (*journey_id)(void *ctx);
} XmipHost;

/* ===================================================================== */
/* 7. Module handle and entrypoint                                       */
/* ===================================================================== */

typedef struct {
    XmipModuleDescriptor descriptor;

    /* Module-private. Opaque to the host, passed back to every function. */
    void *state;

    /* The trait table named by descriptor.module. The host selects the type
     * by that name; a mismatch is a load-time rejection, not a cast. */
    const void *vtable;

    /* Detail for the most recent failing call on this instance. Borrowed,
     * valid until the next call. Empty is legal. */
    XmipStr (*last_error)(void *state);

    void (*destroy)(void *state);
} XmipModule;

/*
 * The one exported symbol. Named "xmip_create_module_v1".
 *
 * The host passes a XmipHost that outlives the module. The module fills *out
 * and returns XMIP_OK, or returns a status and leaves *out untouched. A module
 * that cannot support the host's abi_version returns XMIP_E_UNSUPPORTED here
 * rather than failing later.
 */
typedef XmipStatus (*XmipCreateModuleFn)(const XmipHost *host, XmipModule *out);

/* ===================================================================== */
/* 8. Vtable header                                                      */
/* ===================================================================== */

/*
 * Every trait table begins with this, so the host can drive configuration and
 * lifecycle without knowing which trait it holds.
 *
 * configure is called once, before start, with the artifact's TOML fragment.
 * start and stop may be called repeatedly in that order. Calls on the trait
 * itself are only legal between start and stop.
 */
typedef struct {
    uint32_t   trait_major;
    uint32_t   trait_minor;
    XmipStatus (*configure)(void *state, XmipStr toml);
    XmipStatus (*start)(void *state);
    XmipStatus (*stop)(void *state);
} XmipVtableHeader;

/* ===================================================================== */
/* 9. Trait: transport                                                   */
/* ===================================================================== */

#define XMIP_DIR_RECEIVE  1u
#define XMIP_DIR_SEND     2u

/*
 * Where a receiving transport hands a stream to the host. reply is NULL when
 * the transport has no reply channel; a transport that has one must supply it
 * even if the artifact turns out not to use it.
 */
typedef struct {
    void *ctx;
    XmipStatus (*deliver)(void *ctx,
                          const XmipReader *body,
                          XmipStr peer,
                          const XmipWriter *reply);
} XmipDeliverySink;

/*
 * Direction-neutral, per ADR-0010. One module may declare both directions;
 * the artifact decides which is used. HTTP is the same protocol whether Xmip
 * is listening or calling.
 */
typedef struct {
    XmipVtableHeader header;

    uint32_t directions;    /* XMIP_DIR_RECEIVE | XMIP_DIR_SEND */

    /* Receive side. Called after configure, before start. The sink outlives
     * the module. Returns XMIP_E_UNSUPPORTED if RECEIVE is not declared. */
    XmipStatus (*set_sink)(void *state, const XmipDeliverySink *sink);

    /* Send side. reply is NULL when the artifact is one-way. Returns
     * XMIP_E_UNSUPPORTED if SEND is not declared. */
    XmipStatus (*send)(void *state,
                       const XmipReader *body,
                       const XmipWriter *reply);
} XmipTransportVtable;

/* ===================================================================== */
/* 10. Trait: message                                                    */
/* ===================================================================== */

typedef enum {
    XMIP_NODE_NULL     = 0,
    XMIP_NODE_BOOL     = 1,
    XMIP_NODE_NUMBER   = 2,
    XMIP_NODE_STRING   = 3,
    XMIP_NODE_BINARY   = 4,
    XMIP_NODE_SEQUENCE = 5,
    XMIP_NODE_MAP      = 6
} XmipNodeKind;

/* An opaque handle into a parsed tree. Meaningful only to the module that
 * produced it, and only while its root is alive. */
typedef struct {
    void    *ctx;
    uint64_t id;
} XmipNode;

/*
 * A content handler. It parses a representation into a tree and writes a tree
 * back out. It does not address into the tree - that is the path trait - and
 * it does not judge the tree - that is the contract trait.
 */
typedef struct {
    XmipVtableHeader header;

    /* Cheap sniff for content negotiation, over the head of the stream.
     * out_confidence is 0-100. Must not block and must not allocate. */
    XmipStatus (*probe)(void *state, XmipSlice head, uint8_t *out_confidence);

    XmipStatus (*parse)(void *state, const XmipReader *in, XmipNode *out_root);
    XmipStatus (*write)(void *state, XmipNode root, const XmipWriter *out);
    void       (*release)(void *state, XmipNode root);

    /* Tree access. Names are borrowed and valid while the root is alive. */
    XmipNodeKind (*kind)(void *state, XmipNode n);
    XmipStatus   (*value)(void *state, XmipNode n, XmipStr *out);
    XmipStatus   (*child_count)(void *state, XmipNode n, size_t *out);
    XmipStatus   (*child_at)(void *state, XmipNode n, size_t i,
                             XmipStr *out_name, XmipNode *out_child);
} XmipMessageVtable;

/* ===================================================================== */
/* 11. Trait: path                                                       */
/* ===================================================================== */

/*
 * A path module never parses. It is handed the message module's table and a
 * root, and it walks whatever representation that table exposes. This is what
 * makes XPath over JSON and JSONPath over XML possible, and it is the whole
 * reason Contract, Message and Path are three traits and not one.
 *
 * evaluate fills up to cap results and reports the true count in out_len. If
 * out_len exceeds cap the caller re-calls with a larger buffer; the module
 * must not allocate on the caller's behalf.
 */
typedef struct {
    XmipVtableHeader header;

    XmipStatus (*compile)(void *state, XmipStr expression, void **out_expr);
    void       (*release)(void *state, void *expr);

    XmipStatus (*evaluate)(void *state, void *expr,
                           const XmipMessageVtable *tree, void *tree_state,
                           XmipNode root,
                           XmipNode *out, size_t cap, size_t *out_len);
} XmipPathVtable;

/* ===================================================================== */
/* 12. Trait: contract                                                   */
/* ===================================================================== */

/*
 * A contract judges a stream against a standard. `descriptor` is whatever
 * identifies the contract in that standard's own terms - a schema document, a
 * profile URL, a resource name - and the module interprets it.
 *
 * implies answers what the contract already determines, so an artifact does
 * not have to restate it. A FHIR contract implies its message representation;
 * an EDI X12 contract implies its delimiters. Returns XMIP_E_NOT_FOUND where
 * the standard implies nothing about the key.
 */
typedef struct {
    XmipVtableHeader header;

    XmipStatus (*load)(void *state, XmipStr descriptor, void **out_contract);
    void       (*release)(void *state, void *contract);

    /* Diagnostics are borrowed and valid until the next call on this
     * instance. A valid stream returns XMIP_OK with out_len 0. */
    XmipStatus (*validate)(void *state, void *contract,
                           const XmipReader *in,
                           const XmipDiagnostic **out, size_t *out_len);

    XmipStatus (*implies)(void *state, void *contract,
                          XmipStr key, XmipStr *out);
} XmipContractVtable;

#ifdef __cplusplus
}  /* extern "C" */
#endif

#endif  /* XMIP_MODULE_H */
