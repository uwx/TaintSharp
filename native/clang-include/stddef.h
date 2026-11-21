/* Minimal ClangSharp input shim; see stdbool.h in this directory. */
#ifndef TAINTSHARP_BINDINGS_STDDEF_H
#define TAINTSHARP_BINDINGS_STDDEF_H
typedef __SIZE_TYPE__ size_t;
typedef __PTRDIFF_TYPE__ ptrdiff_t;
typedef __WCHAR_TYPE__ wchar_t;
#define NULL ((void *)0)
#endif
