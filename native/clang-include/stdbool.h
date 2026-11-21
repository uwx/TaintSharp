/* Minimal ClangSharp input shim: the generator's bundled libclang does not
   ship compiler builtin headers. Only what taint.h needs is defined. */
#ifndef TAINTSHARP_BINDINGS_STDBOOL_H
#define TAINTSHARP_BINDINGS_STDBOOL_H
#define bool _Bool
#define true 1
#define false 0
#define __bool_true_false_are_defined 1
#endif
