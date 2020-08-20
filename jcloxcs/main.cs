using System.Collections.Generic;

namespace lox
{
    using util;

    class main
    {
        static InterpretResult run(char[] source)
        {
            List<Token> tokens = Scanner.scanTokens(source);

            Parser parser = new Parser(tokens);
            List<Stmt> statements = parser.parse();

            if (Parser.hadError)
            {
                return InterpretResult.INTERPRET_PARSE_ERROR;
            }

            if (flag.InterpreterType == InterpreterType.JLOX_INTERPRETER)
                return jlox.Interpreter.interpret(statements);
            else
                return clox.VM.interpret(statements);
        }

        static string readFile(string path)
        {
            System.Text.StringBuilder buffer = null;
            if (!System.IO.File.Exists(path))
            {
                System.Console.WriteLine("Could not open file {0}.", path);
                System.Environment.Exit(74);
            }
            buffer = new System.Text.StringBuilder(System.IO.File.ReadAllText(path));
            buffer.Append('\0');
            if (buffer == null)
            {
                System.Console.WriteLine("Not enough memory to read {0}.", path);
                System.Environment.Exit(74);
            }
            return buffer.ToString();
        }

        static void repl()
        {
            string line;
            for (;;)
            {
                System.Console.Write("> ");
                line = System.Console.ReadLine() + '\0';
                if (line == null)
                {
                    System.Console.WriteLine();
                    break;
                }

                run(line.ToCharArray());
            }
        }

        private static void runFile(string path)
        {
            char[] source = readFile(path).ToCharArray();

            InterpretResult result = run(source);

            clox.Memory.FREE<char>(ref source);

            if (result != InterpretResult.INTERPRET_OK)
                System.Environment.Exit((int)result);
        }

        static void Main(string[] args)
        {
            flag.Parse(args);

            if (flag.InterpreterType == InterpreterType.CLOX_BYTECODE_VM)
                clox.VM.initVM();

            if (flag.RunType == RunType.RUN_REPL)
            {
                repl();
            }
            else
            {
                runFile(args[flag.ArgsFileIndex]);
            }

            if (flag.InterpreterType == InterpreterType.CLOX_BYTECODE_VM)
                clox.VM.freeVM();
#if DEBUG
            System.Console.WriteLine("\n\nPress a key to exit.");
            System.Console.ReadKey();
#endif
        }
    }
}
