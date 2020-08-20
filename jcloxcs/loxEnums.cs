namespace lox
{
    // shared by jloxResolver and cloxCompiler
    public enum FunctionType
    {
        TYPE_NONE,
        TYPE_FUNCTION,
        TYPE_INITIALIZER,
        TYPE_METHOD,
        TYPE_SCRIPT
    }

    public enum InterpretResult
    {
        INTERPRET_OK = 0,
        INTERPRET_PARSE_ERROR = 50,
        INTERPRET_RESOLVE_ERROR = 60,
        INTERPRET_COMPILE_ERROR = 65,
        INTERPRET_RUNTIME_ERROR = 70,
    }

    public enum InterpreterType
    {
        CLOX_BYTECODE_VM,
        JLOX_INTERPRETER,
    }

    public enum RunType
    {
        RUN_FILE,
        RUN_REPL,
    }
}
