# jcloxcs


An implementation of [Crafting Interpreters](https://craftinginterpreters.com/)’ [lox](https://github.com/munificent/craftinginterpreters) programming language that uses the parse tree from [jlox](https://github.com/drewbanas/jloxcs) to generate bytecode for the [clox](https://github.com/drewbanas/cloxcs) virtual machine. Coded in C#.

## Usage:
`jcloxcs.exe [ scriptFile.lox ] [ -j ]`
* Arguments are optional and the interpreter goes into REPL mode if no script file is specified.
* -j : use this switch to use jlox instead of clox.
* Don’t type the square brackets.

## Major changes:
* The clox compiler takes in a list of statements just like the jlox interpreter. It then uses the visitor pattern to emit bytecode as it walks the parse tree.
* The parser from jlox is modified to use a Pratt parser for expressions, similar to how it is done in clox.
* The interpreter can be chosen between the clox (default) and jlox (__-j__ command line switch).

## Name spaces and file naming
* __namespace lox, lox*.cs__: code shared by both jlox and clox. This includes the parser and some enum types that are now shared.
* __namespace clox, clox*.cs__: code that belongs to the clox bytecode interpreter. This includes the compiler, the virtual machine and other data types used by clox.
* __namespace jlox, jlox*.cs__: code that belongs to the jlox interpreter. This includes the resolver, the interpreter itself, and other class types used by jlox.

## Other changes
* Errors are reported a bit differently since not all tokens are stored in the parse tree. So error lines are just approximate in error messages (“near” is used instead of “at”).
* Since the clox scanner is used, lexemes/literals are not stored as separate strings. Getting the lexeme string now requires calling a method, instead of accessing a field.
* Some code that was originally in jlox’s main class had been merged into the jlox interpreter.
* Some bugs in previous implementations were discovered and fixed in jcloxcs. Particularly, in error handling involving hashmaps, how types are printed in jlox and differences in printed error messages.
