using System.Collections.Generic;

namespace jlox
{
    interface LoxCallable
    {
        int arity();
        object call(Interpreter interpreter, List<object> arguments);
    }
}
