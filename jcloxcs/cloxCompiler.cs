#if DEBUG
#define DEBUG_PRINT_CODE // clox common.h
#endif

#define NAN_BOXING
#if NAN_BOXING
using Value_t = System.UInt64;
#endif

using System.Collections.Generic;
using System;

namespace clox
{
    using lox;

    public struct Local
    {
        public Token name;
        public int depth;
        public bool isCaptured;
    }

    public struct Upvalue
    {
        public byte index;
        public bool isLocal;
    }

    public class Compiler_t
    {
        public Compiler_t enclosing; // Csharp struct doesn't allow recursive defintion
        public ObjFunction function;
        public FunctionType type;

        public Local[] locals = new Local[Compiler.UINT8_COUNT];
        public int localCount;
        public Upvalue[] upvalues = new Upvalue[Compiler.UINT8_COUNT];
        public int scopeDepth;
    }

    public class ClassCompiler
    {
        public ClassCompiler enclosing;
        public Token name;
        public bool hasSuperclass;
    }

    class Compiler : Expr.Visitor<object>, Stmt.Visitor<object>
    {
        public const int UINT8_COUNT = (int)byte.MaxValue + 1;

        static Compiler_t current = null;
        static ClassCompiler currentClass = null;


        static List<Stmt> statements;
        static Token nearbyToken; // not all Expr and Stmt have tokens in them, so complete line information is lost, it can only get updated to something nearby

        public static bool hadError = false;


        public Compiler(List<Stmt> stmts)
        {
            statements = stmts;
        }

        private static Chunk_t currentChunk()
        {
            return current.function.chunk;
        }

        static void captureToken(Token token)
        {
            nearbyToken = token;
        }

        private static void errorAt(Token token, string message)
        {
            System.Console.Write("[line {0}] Error", token.line.ToString());

            if (token.type == TokenType.TOKEN_EOF)
            {
                System.Console.Write(" at end");
            }
            else if (token.type == TokenType.TOKEN_ERROR)
            {
                // Nothing.
            }
            else
            {
                // replaced 'at' to 'near', since captured tokens just approximate the error's line
                System.Console.Write(" near '{0}'", new string(token._char_ptr, token.start, token.length));
            }

            System.Console.WriteLine(": {0}", message);
            hadError = true;
        }

        private static void error(string message)
        {
            errorAt(nearbyToken, message);
        }

        private static void errorAtCurrent(string message)
        {
            errorAt(nearbyToken, message);
        }

        private static void emitByte(byte byte_)
        {
            Chunk_t _chunk = currentChunk();
            Chunk.writeChunk(ref _chunk, byte_, nearbyToken.line);

            current.function.chunk = _chunk; // workaround
        }
        private static void emitByte(OpCode op) { emitByte((byte)op); }

        private static void emitBytes(byte byte1, byte byte2)
        {
            emitByte(byte1);
            emitByte(byte2);
        }
        private static void emitBytes(OpCode op1, OpCode op2) { emitBytes((byte)op1, (byte)op2); }

        private static void emitLoop(int loopStart)
        {
            emitByte(OpCode.OP_LOOP);

            int offset = currentChunk().count - loopStart + 2;
            if (offset > ushort.MaxValue)
                error("Loop body too large.");

            emitByte((byte)((offset >> 8) & 0xff));
            emitByte((byte)(offset & 0xff));
        }

        private static int emitJump(byte instruction)
        {
            emitByte(instruction);
            emitByte(0xff);
            emitByte(0xff);
            return currentChunk().count - 2;
        }
        private static int emitJump(OpCode instruction) { return emitJump((byte)instruction); }

        private static void emitReturn()
        {
            if (current.type == FunctionType.TYPE_INITIALIZER)
            {
                emitBytes((byte)OpCode.OP_GET_LOCAL, 0);
            }
            else
            {
                emitByte(OpCode.OP_NIL);
            }

            emitByte(OpCode.OP_RETURN);
        }

        private static byte makeConstant(Value_t value)
        {
            Chunk_t _chunk = currentChunk();
            int constant = Chunk.addConstant(ref _chunk, value);
            if (constant > byte.MaxValue)
            {
                error("Too many constants in one chunk.");
                return 0;
            }

            current.function.chunk = _chunk; // work around
            return (byte)constant;
        }

        private static void emitConstant(Value_t value)
        {
            emitBytes((byte)OpCode.OP_CONSTANT, makeConstant(value));
        }

        private static void patchJump(int offset)
        {
            int jump = currentChunk().count - offset - 2;

            if (jump > ushort.MaxValue)
            {
                error("Too much code to jump over.");
            }


            currentChunk().code[offset] = (byte)((jump >> 8) & 0xff);
            currentChunk().code[offset + 1] = (byte)(jump & 0xff);
        }
        
        private static void initCompiler(ref Compiler_t compiler, FunctionType type, Token funcName)
        {
            compiler.enclosing = current;
            compiler.function = null;
            compiler.type = type;
            compiler.localCount = 0;
            compiler.scopeDepth = 0;
            compiler.function = Object.newFunction();
            current = compiler;

            if (type != FunctionType.TYPE_SCRIPT)
            {
                current.function.name = Object.copyString(funcName._char_ptr, funcName.start, funcName.length);
            }

            Local local = current.locals[current.localCount++];
            local.depth = 0;
            local.isCaptured = false;

            if (type != FunctionType.TYPE_FUNCTION)
            {
                local.name._char_ptr = "this\0".ToCharArray();
                local.name.start = 0;
                local.name.length = 4;
            }
            else
            {
                local.name._char_ptr = new char[] { '\0' };
                local.name.start = 0;
                local.name.length = 0;
            }

            current.locals[current.localCount - 1] = local; // C sharp fix.
        }

        private static ObjFunction endCompiler()
        {
            emitReturn();
            ObjFunction function = current.function;

#if DEBUG_PRINT_CODE
            if (!hadError)
            {
                Chunk_t _chunk = currentChunk();
                Debug.disassembleChunk(ref _chunk, function.name != null ? function.name.chars : "<script>\0".ToCharArray());
            }
#endif

            current = current.enclosing;
            return function;
        }

        private static void beginScope()
        {
            current.scopeDepth++;
        }

        private static void endScope()
        {
            current.scopeDepth--;

            while (current.localCount > 0 && current.locals[current.localCount - 1].depth > current.scopeDepth)
            {
                if (current.locals[current.localCount - 1].isCaptured)
                {
                    emitByte(OpCode.OP_CLOSE_UPVALUE);
                }
                else
                {
                    emitByte(OpCode.OP_POP);
                }
                current.localCount--;
            }
        }

        private static byte identifierConstant(Token name)
        {
            return makeConstant(Value.OBJ_VAL(Object.copyString(name._char_ptr, name.start, name.length)));
        }

        static bool identifiersEqual(Token a, Token b)
        {
            if (a.length != b.length)
                return false;

            return util.util._memcmp(a._char_ptr, a.start, b._char_ptr, b.start, a.length);
        }

        private static int resolveLocal(ref Compiler_t compiler, Token name)
        {
            for (int i = compiler.localCount - 1; i >= 0; i--)
            {
                Local local = compiler.locals[i];
                if (identifiersEqual(name, local.name))
                {
                    if (local.depth == -1)
                    {
                        error("Cannot read local variable in its own initializer.");
                    }
                    return i;
                }
            }
            return -1;
        }

        private static int addUpvalue(ref Compiler_t compiler, byte index, bool isLocal)
        {
            int upvalueCount = compiler.function.upvalueCount;

            for (int i = 0; i < upvalueCount; i++)
            {
                Upvalue upvalue = compiler.upvalues[i];
                if (upvalue.index == index && upvalue.isLocal == isLocal)
                {
                    return i;
                }
            }

            if (upvalueCount == UINT8_COUNT)
            {
                error("Too many closure variables in function.");
                return 0;
            }

            compiler.upvalues[upvalueCount].isLocal = isLocal;
            compiler.upvalues[upvalueCount].index = index;
            return compiler.function.upvalueCount++;
        }

        private static int resolveUpvalue(ref Compiler_t compiler, Token name)
        {
            if (compiler.enclosing == null)
                return -1;

            int local = resolveLocal(ref compiler.enclosing, name);
            if (local != -1)
            {
                compiler.enclosing.locals[local].isCaptured = true;
                return addUpvalue(ref compiler, (byte)local, true);
            }

            int upvalue = resolveUpvalue(ref compiler.enclosing, name);
            if (upvalue != -1)
            {
                return addUpvalue(ref compiler, (byte)upvalue, false);
            }

            return -1;
        }

        private static void addLocal(Token name)
        {
            if (current.localCount == UINT8_COUNT)
            {
                error("Too many local variables in function.");
                return;
            }

            Local local = current.locals[current.localCount++];
            local.name = name;
            local.depth = -1;
            local.isCaptured = false;

            current.locals[current.localCount - 1] = local; // Csharp fix.
        }

        private static void markInitialized()
        {
            if (current.scopeDepth == 0)
                return;
            current.locals[current.localCount - 1].depth = current.scopeDepth;
        }

        private static void defineVariable(byte global)
        {
            if (current.scopeDepth > 0)
            {
                markInitialized();
                return;
            }

            emitBytes((byte)OpCode.OP_DEFINE_GLOBAL, global);
        }

        private static Token syntheticToken(string text)
        {
            Token token = new Token();
            token.start = 0;
            token.length = text.Length;

            token._char_ptr = text.ToCharArray();
            return token;
        }

        public static void markCompilerRoots()
        {
            Compiler_t compiler = current;
            while (compiler != null)
            {
                Memory.markObject((Obj)compiler.function);
                compiler = compiler.enclosing;
            }
        }

        void function(Stmt.Function funStmt, FunctionType type)
        {
            /* clox: funDeclaration */
            byte global = 0;
            if (type == FunctionType.TYPE_FUNCTION)
            {
                global = parseVariable(funStmt.name, "Expect function name.");
                markInitialized();
            }


            /* clox: function */
            Compiler_t compiler = new Compiler_t();
            initCompiler(ref compiler, type, funStmt.name);

            captureToken(funStmt.name);

            beginScope();

            // Compile parameter list.
            if (funStmt.params_.Count > 255)
            {
                errorAtCurrent("Cannot have more than 255 parameters.");
            }
            foreach (Token param in funStmt.params_)
            {
                byte paramConstant = parseVariable(param, "Expect parameter name.");
                defineVariable(paramConstant);
            }
            current.function.arity = funStmt.params_.Count;


            // Compile the function body.
            foreach (Stmt stmt in funStmt.body)
                compile(stmt);

            // Create the function object.
            ObjFunction function = endCompiler();
            emitBytes((byte)OpCode.OP_CLOSURE, makeConstant(Value.OBJ_VAL(function)));

            for (int i = 0; i < function.upvalueCount; i++)
            {
                emitByte((byte)(compiler.upvalues[i].isLocal ? 1 : 0));
                emitByte(compiler.upvalues[i].index);
            }

            /* clox: funDeclaration */
            if (type == FunctionType.TYPE_FUNCTION)
            {
                defineVariable(global);
            }
        }

        private static void declareVariable(Token name)
        {
            // Global variables are implicitly declared.
            if (current.scopeDepth == 0)
                return;

            for (int i = current.localCount - 1; i >= 0; i--)
            {
                Local local = current.locals[i];
                if (local.depth != -1 && local.depth < current.scopeDepth)
                {
                    break;
                }

                if (identifiersEqual(name, local.name))
                {
                    error("Variable with this name already declared in this scope.");
                }
            }

            addLocal(name);
        }

        static byte parseVariable(Token name, string errorMessage)
        {
            declareVariable(name);
            if (current.scopeDepth > 0)
                return 0;

            return identifierConstant(name);
        }

        byte compile_argumentList(List<Expr> arguments)
        {
            if (arguments.Count > 255)
            {
                error("Cannot have more than 255 arguments.");
            }

            foreach (Expr arg in arguments)
            {
                compile(arg);
            }
            return (byte)arguments.Count;
        }

        void getCall(Expr.Call gCallExpr)
        {
            Expr.Get getExpr = (Expr.Get)gCallExpr.callee;
            compile(getExpr.object_);

            captureToken(getExpr.name);

            byte name = identifierConstant(getExpr.name);
            byte argCount = compile_argumentList(gCallExpr.arguments);
            emitBytes((byte)OpCode.OP_INVOKE, name);
            emitByte(argCount);
        }

        static void getNamedVariable(Token name)
        {
            OpCode getOp;
            int arg = resolveLocal(ref current, name);
            if (arg != -1)
            {
                getOp = OpCode.OP_GET_LOCAL;
            }
            else if ((arg = resolveUpvalue(ref current, name)) != -1)
            {
                getOp = OpCode.OP_GET_UPVALUE;
            }
            else
            {
                arg = identifierConstant(name);
                getOp = OpCode.OP_GET_GLOBAL;
            }

            emitBytes((byte)getOp, (byte)arg);
        }

        void setNamedVariable(Token name, Expr value)
        {
            OpCode setOp;

            int arg = resolveLocal(ref current, name);
            if (arg != -1)
            {
                setOp = OpCode.OP_SET_LOCAL;
            }
            else if ((arg = resolveUpvalue(ref current, name)) != -1)
            {
                setOp = OpCode.OP_SET_UPVALUE;
            }
            else
            {
                arg = identifierConstant(name);
                setOp = OpCode.OP_SET_GLOBAL;
            }

            compile(value);
            emitBytes((byte)setOp, (byte)arg);
        }

        void superCall(Expr.Call sCallExpr)
        {
            Expr.Super superExpr = (Expr.Super)sCallExpr.callee;

            captureToken(superExpr.keyword);

            if (currentClass == null)
            {
                error("Cannot use 'super' outside of a class.");
            }
            else if (!currentClass.hasSuperclass)
            {
                error("Cannot use 'super' in a class with no superclass.");
            }

            captureToken(superExpr.method);

            byte name = identifierConstant(superExpr.method);

            getNamedVariable(syntheticToken("this"));

            byte argCount = compile_argumentList(sCallExpr.arguments);
            getNamedVariable(superExpr.keyword);

            emitBytes((byte)OpCode.OP_SUPER_INVOKE, name);
            emitByte(argCount);
        }

        /***  VISITOR COMPILER  ***/

        public ObjFunction compile()
        {

            Compiler_t compiler = new Compiler_t();
            Token t = new Token { line = 1 };
            initCompiler(ref compiler, FunctionType.TYPE_SCRIPT, t);

            captureToken(t);

            foreach (Stmt stmt in statements)
            {
                compile(stmt);
            }

            ObjFunction function = endCompiler();
            return hadError ? null : function; 
        }

        void compile(Stmt stmt)
        {
            stmt.accept(this);
        }

        void compile(Expr expr)
        {
            expr.accept(this);
        }

        public object visitBlockStmt(Stmt.Block blockStmt)
        {
            beginScope();
            foreach (Stmt stmt in blockStmt.statements)
            {
                compile(stmt);
            }
            endScope();
            return null;
        }

        public object visitClassStmt(Stmt.Class klass)
        {
            Token className = klass.name;
            captureToken(className);

            byte nameConstant = identifierConstant(className);

            declareVariable(className);

            emitBytes((byte)OpCode.OP_CLASS, nameConstant);
            defineVariable(nameConstant);

            ClassCompiler classCompiler = new ClassCompiler();
            classCompiler.name = className;
            classCompiler.hasSuperclass = false;
            classCompiler.enclosing = currentClass;
            currentClass = classCompiler;

            if (klass.superclass != null)
            {
                Token superName = klass.superclass.name;
                getNamedVariable(superName);

                if (identifiersEqual(className, superName))
                {
                    error("A class cannot inherit from itself.");
                }

                beginScope();
                addLocal(syntheticToken("super"));
                defineVariable(0);

                getNamedVariable(className);
                emitByte(OpCode.OP_INHERIT);
                classCompiler.hasSuperclass = true;
                currentClass = classCompiler; // CS ref fix
            }

            getNamedVariable(className);


            // stuff done in clox: method()
            foreach (Stmt.Function method in klass.methods)
            {
                Token methodName = method.name;

                captureToken(methodName);

                byte constant = identifierConstant(methodName);
                FunctionType type = FunctionType.TYPE_METHOD;

                if (methodName.length == 4 && util.util._memcmp(methodName._char_ptr, methodName.start, "init", 4))
                {
                    type = FunctionType.TYPE_INITIALIZER;
                }

                function(method, type);
                emitBytes((byte)OpCode.OP_METHOD, constant);
            }
            emitByte(OpCode.OP_POP);

            if (classCompiler.hasSuperclass)
            {
                endScope();
            }

            currentClass = currentClass.enclosing;

            return null;
        }

        public object visitExprssionStmt(Stmt.Expression stmt)
        {
            compile(stmt.expression);
            emitByte(OpCode.OP_POP);
            return null;
        }

        public object visitFunctionStmt(Stmt.Function stmt)
        {
            function(stmt, FunctionType.TYPE_FUNCTION);
            return null;
        }

        public object visitIfStmt(Stmt.If ifStmt)
        {
            compile(ifStmt.condition);

            int thenJump = emitJump((byte)OpCode.OP_JUMP_IF_FALSE);
            emitByte(OpCode.OP_POP);
            compile(ifStmt.thenBranch);

            int elseJump = emitJump((byte)OpCode.OP_JUMP);

            patchJump(thenJump);
            emitByte(OpCode.OP_POP);

            if (ifStmt.elseBranch != null)
                compile(ifStmt.elseBranch);

            patchJump(elseJump);

            return null;
        }

        public object visitPrintStmt(Stmt.Print printStmt)
        {
            compile(printStmt.expression);
            emitByte(OpCode.OP_PRINT);
            return null;
        }

        public object visitReturnStmt(Stmt.Return returnStmt)
        {
            captureToken(returnStmt.keyword);

            if (current.type == FunctionType.TYPE_SCRIPT)
            {
                error("Cannot return from top-level code.");
            }

            if (returnStmt.value == null)
            {
                emitReturn();
            }
            else
            {
                if (current.type == FunctionType.TYPE_INITIALIZER)
                {
                    error("Cannot return a value from an initializer.");
                }

                compile(returnStmt.value);
                emitByte(OpCode.OP_RETURN);
            }

            return null;
        }

        public object visitVarStmt(Stmt.Var varDec)
        {
            captureToken(varDec.name);

            byte global = parseVariable(varDec.name, "Expect variable name.");

            if (varDec.initializer != null)
            {
                compile(varDec.initializer);
            }
            else
            {
                emitByte(OpCode.OP_NIL); // null initializer
            }

            defineVariable(global);
            return null;
        }

        public object visitWhileStmt(Stmt.While whileStmt)
        {
            int loopStart = currentChunk().count;
            compile(whileStmt.condition);

            int exitJump = emitJump(OpCode.OP_JUMP_IF_FALSE);

            emitByte(OpCode.OP_POP);
            compile(whileStmt.body);// clox: statement();

            emitLoop(loopStart);

            patchJump(exitJump);
            emitByte(OpCode.OP_POP);

            return null;
        }

        public object visitAssignExpr(Expr.Assign expr)
        {
            captureToken(expr.name);
            setNamedVariable(expr.name, expr.value);
            return null;
        }

        public object visitBinaryExpr(Expr.Binary binExpr)
        {
            // Remember the operator.
            TokenType operatorType = binExpr.operator_.type;
            captureToken(binExpr.operator_);

            compile(binExpr.left);
            compile(binExpr.right);

            // Emit the operator instruction.
            switch (operatorType)
            {
                case TokenType.TOKEN_BANG_EQUAL:
                    emitBytes(OpCode.OP_EQUAL, OpCode.OP_NOT);
                    break;
                case TokenType.TOKEN_EQUAL_EQUAL:
                    emitByte(OpCode.OP_EQUAL);
                    break;
                case TokenType.TOKEN_GREATER:
                    emitByte(OpCode.OP_GREATER);
                    break;
                case TokenType.TOKEN_GREATER_EQUAL:
                    emitBytes(OpCode.OP_LESS, OpCode.OP_NOT);
                    break;
                case TokenType.TOKEN_LESS:
                    emitByte(OpCode.OP_LESS);
                    break;
                case TokenType.TOKEN_LESS_EQUAL:
                    emitBytes(OpCode.OP_GREATER, OpCode.OP_NOT);
                    break;
                case TokenType.TOKEN_PLUS:
                    emitByte(OpCode.OP_ADD);
                    break;
                case TokenType.TOKEN_MINUS:
                    emitByte(OpCode.OP_SUBTRACT);
                    break;
                case TokenType.TOKEN_STAR:
                    emitByte(OpCode.OP_MULTIPLY);
                    break;
                case TokenType.TOKEN_SLASH:
                    emitByte(OpCode.OP_DIVIDE);
                    break;
                default:
                    return false; // Unreachable.                              
            }

            return true;
        }

        /*
         * Expr.Get and Expr.Super callees are intercepted
         * for separate treatment for invoke and property
         */
        public object visitCallExpr(Expr.Call callExpr)
        {
            captureToken(callExpr.paren);
            byte argCount;

            if (callExpr.callee is Expr.Variable) // "common"/global functions
            {

                getNamedVariable(((Expr.Variable)callExpr.callee).name);

                argCount = compile_argumentList(callExpr.arguments);
                emitBytes((byte)OpCode.OP_CALL, argCount);

                return null;
            }

            if (callExpr.callee is Expr.Get) // methods
            {
                getCall(callExpr);
                return null;
            }

            if (callExpr.callee is Expr.Super) // super methods
            {
                superCall(callExpr);
                return null;
            }

            if (callExpr.callee is Expr.Call) // nested calls
            {
                argCount = compile_argumentList(callExpr.arguments);
                visitCallExpr((Expr.Call)callExpr.callee);

                emitBytes((byte)OpCode.OP_CALL, argCount);
                return null;
            }

            error("Can only call functions and classes."); // runtime error in clox

            return null;
        }

        public object visitGetExpr(Expr.Get getExpr)
        {
            compile(getExpr.object_);

            captureToken(getExpr.name);

            byte name = identifierConstant(getExpr.name);
            emitBytes((byte)OpCode.OP_GET_PROPERTY, name);
            return null;
        }

        public object visitGroupingExpr(Expr.Grouping expr)
        {
            compile(expr.expression);
            return null;
        }

        public object visitLiteralExpr(Expr.Literal litExpr)
        {
            if (litExpr.value is bool)
            {
                if ((bool)litExpr.value)
                    emitByte(OpCode.OP_TRUE);
                else
                    emitByte(OpCode.OP_FALSE);

                return true;
            }
            else if (litExpr.value is double)
            {
                emitConstant(Value.NUMBER_VAL((double)litExpr.value));
                return true;
            }
            else if (litExpr.value is string)
            {
                char[] chars = ((string)litExpr.value).ToCharArray();
                emitConstant(Value.OBJ_VAL(Object.copyString(chars, 0, chars.Length)));
                return true;
            }
            else if (litExpr.value == null)
            {
                emitByte(OpCode.OP_NIL);
                return true;
            }

            return null;
        }

        public object visitLogicalExpr(Expr.Logical logicExpr)
        {
            captureToken(logicExpr.operator_);

            compile(logicExpr.left);
            if (logicExpr.operator_.type == TokenType.TOKEN_AND)
            {
                int endJump = emitJump((byte)OpCode.OP_JUMP_IF_FALSE);

                emitByte(OpCode.OP_POP);
                compile(logicExpr.right);
                patchJump(endJump);
            }
            else if (logicExpr.operator_.type == TokenType.TOKEN_OR)
            {
                int elseJump = emitJump(OpCode.OP_JUMP_IF_FALSE);
                int endJump = emitJump(OpCode.OP_JUMP);

                patchJump(elseJump);
                emitByte(OpCode.OP_POP);

                compile(logicExpr.right);
                patchJump(endJump);
            }

            return null;
        }

        public object visitSetExpr(Expr.Set setExpr)
        {
            compile(setExpr.object_);
            compile(setExpr.value);

            captureToken(setExpr.name);

            byte name = identifierConstant(setExpr.name);
            emitBytes((byte)OpCode.OP_SET_PROPERTY, name);

            return null;
        }

        public object visitSuperExpr(Expr.Super superExpr)
        {
            captureToken(superExpr.keyword);

            if (currentClass == null)
            {
                error("Cannot use 'super' outside of a class.");
            }
            else if (!currentClass.hasSuperclass)
            {
                error("Cannot use 'super' in a class with no superclass.");
            }

            captureToken(superExpr.method);

            byte name = identifierConstant(superExpr.method);
            getNamedVariable(syntheticToken("this"));

            getNamedVariable(syntheticToken("super"));
            emitBytes((byte)OpCode.OP_GET_SUPER, name);

            return null;
        }

        public object visitThisExpr(Expr.This thisExpr)
        {
            captureToken(thisExpr.keyword);

            if (currentClass == null)
            {
                error("Cannot use 'this' outside of a class.");
                return null;
            }

            getNamedVariable(thisExpr.keyword);

            return null;
        }

        public object visitUnaryExpr(Expr.Unary unaryExpr)
        {
            captureToken(unaryExpr.operator_);

            TokenType operatorType = unaryExpr.operator_.type;

            compile(unaryExpr.right);

            // Emit the operator instruction.
            switch (operatorType)
            {
                case TokenType.TOKEN_BANG:
                    emitByte(OpCode.OP_NOT);
                    break;
                case TokenType.TOKEN_MINUS:
                    emitByte(OpCode.OP_NEGATE);
                    break;
                default:
                    return null; // Unreachable.
            }
            return null;
        }

        public object visitVariableExpr(Expr.Variable varExpr)
        {
            captureToken(varExpr.name);

            getNamedVariable(varExpr.name);
            return null;
        }

    }
}
