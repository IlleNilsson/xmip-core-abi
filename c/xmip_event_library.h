/* SPDX-License-Identifier: AGPL-3.0-or-later */
/* Copyright the Xmip authors. */

/*
 * xmip_event_library.h - the runtime's library loaded by path, and the six
 * symbols of xmip_operate.h section 11 looked up once by the names the
 * header gives them.
 *
 * Loading is all this does. Which Events a filter matches, whether a
 * subscriber may see them, the queue and the audit are the runtime's
 * (ADR-0065); a C program calls the function pointers below directly.
 */

#ifndef XMIP_EVENT_LIBRARY_H
#define XMIP_EVENT_LIBRARY_H

#include <string.h>

#include "xmip_operate.h"

#ifdef _WIN32
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  include <windows.h>
#else
#  include <dlfcn.h>
#endif

typedef struct {
    void                  *handle;
    XmipEventSubscribeFn   subscribe;
    XmipEventNextFn        next;
    XmipEventBatchFreeFn   batch_free;
    XmipEventListenFn      listen;
    XmipEventUnsubscribeFn unsubscribe;
    XmipEventPublishFn     publish;
} XmipEventLibrary;

/* A borrowed XmipStr over a null-terminated C string. */
static inline XmipStr xmip_str(const char *text)
{
    XmipStr value;
    value.ptr = (const uint8_t *)text;
    value.len = text ? strlen(text) : 0;
    return value;
}

static inline void *xmip_event_symbol(void *handle, const char *name)
{
#ifdef _WIN32
    return (void *)GetProcAddress((HMODULE)handle, name);
#else
    return dlsym(handle, name);
#endif
}

static inline void xmip_event_library_close(XmipEventLibrary *library)
{
    if (library->handle) {
#ifdef _WIN32
        FreeLibrary((HMODULE)library->handle);
#else
        dlclose(library->handle);
#endif
    }
    memset(library, 0, sizeof *library);
}

/*
 * Load the runtime's library at path and resolve every section 11 symbol.
 * XMIP_OK, or XMIP_E_NOT_FOUND when the library or a symbol is missing,
 * with the library left closed.
 */
static inline XmipStatus xmip_event_library_open(const char *path, XmipEventLibrary *library)
{
    memset(library, 0, sizeof *library);
#ifdef _WIN32
    library->handle = (void *)LoadLibraryA(path);
#else
    library->handle = dlopen(path, RTLD_NOW | RTLD_LOCAL);
#endif
    if (!library->handle) {
        return XMIP_E_NOT_FOUND;
    }
    void *h = library->handle;
    library->subscribe = (XmipEventSubscribeFn)xmip_event_symbol(
        h, XMIP_EVENT_SUBSCRIBE_ENTRYPOINT);
    library->next = (XmipEventNextFn)xmip_event_symbol(h, XMIP_EVENT_NEXT_ENTRYPOINT);
    library->batch_free = (XmipEventBatchFreeFn)xmip_event_symbol(
        h, XMIP_EVENT_BATCH_FREE_ENTRYPOINT);
    library->listen = (XmipEventListenFn)xmip_event_symbol(h, XMIP_EVENT_LISTEN_ENTRYPOINT);
    library->unsubscribe = (XmipEventUnsubscribeFn)xmip_event_symbol(
        h, XMIP_EVENT_UNSUBSCRIBE_ENTRYPOINT);
    library->publish = (XmipEventPublishFn)xmip_event_symbol(h, XMIP_EVENT_PUBLISH_ENTRYPOINT);
    if (!library->subscribe || !library->next || !library->batch_free || !library->listen
        || !library->unsubscribe || !library->publish) {
        xmip_event_library_close(library);
        return XMIP_E_NOT_FOUND;
    }
    return XMIP_OK;
}

#endif /* XMIP_EVENT_LIBRARY_H */
