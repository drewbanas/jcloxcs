using System.Collections.Generic;

namespace lox
{

    class Parser
    {
        private class ParseError : System.SystemException { }

        private readonly List<Token> tokens;
        private int current = 0;

        public static bool hadError = false;

        static Parser parser;


        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        public List<Stmt> parse()
        {
            parser = this; // HACK FIX

            List<Stmt> statements = new List<Stmt>();
            while (!isAtEnd())
            {
                statements.Add(declaration());
            }
            return statements;
        }

        private Stmt declaration()
        {
            try
            {
                if (match(TokenType.TOKEN_CLASS))
                    return classDeclaration();
                if (match(TokenType.TOKEN_FUN))
                    return function("function");
                if (match(TokenType.TOKEN_VAR))
                    return varDeclaration();
                return statement();
            }
            catch (ParseError error)
            {
                syncrhonize();
                return null;
            }
        }

        private Stmt classDeclaration()
        {
            Token name = consume(TokenType.TOKEN_IDENTIFIER, "Expect class name.");

            Expr.Variable superclass = null;
            if (match(TokenType.TOKEN_LESS))
            {
                consume(TokenType.TOKEN_IDENTIFIER, "Expect superclass name.");
                superclass = new Expr.Variable(previous());
            }

            consume(TokenType.TOKEN_LEFT_BRACE, "Expect '{' before class body.");

            List<Stmt.Function> methods = new List<Stmt.Function>();
            while (!check(TokenType.TOKEN_RIGHT_BRACE) && !isAtEnd())
            {
                methods.Add(function("method"));
            }

            consume(TokenType.TOKEN_RIGHT_BRACE, "Expect '}' after class body.");

            return new Stmt.Class(name, superclass, methods);
        }

        private Stmt statement()
        {
            if (match(TokenType.TOKEN_FOR))
                return forStatement();
            if (match(TokenType.TOKEN_IF))
                return ifStatement();
            if (match(TokenType.TOKEN_PRINT))
                return printStatement();
            if (match(TokenType.TOKEN_RETURN))
                return returnStatement();
            if (match(TokenType.TOKEN_WHILE))
                return whileStatement();
            if (match(TokenType.TOKEN_LEFT_BRACE))
                return new Stmt.Block(block());

            return expressionStatement();
        }

        private Stmt forStatement()
        {
            consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");

            Stmt initializer;
            if (match(TokenType.TOKEN_SEMICOLON))
            {
                initializer = null;
            }
            else if (match(TokenType.TOKEN_VAR))
            {
                initializer = varDeclaration();
            }
            else
            {
                initializer = expressionStatement();
            }

            Expr condition = null;
            if (!check(TokenType.TOKEN_SEMICOLON))
            {
                condition = expression();
            }
            consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after loop condition.");

            Expr increment = null;
            if (!check(TokenType.TOKEN_RIGHT_PAREN))
            {
                increment = expression();
            }
            consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");
            Stmt body = statement();

            if (increment != null)
            {
                body = new Stmt.Block(new List<Stmt> { body, new Stmt.Expression(increment) });
            }

            if (condition == null)
                condition = new Expr.Literal(true);
            body = new Stmt.While(condition, body);

            if (initializer != null)
                body = new Stmt.Block(new List<Stmt> { initializer, body });

            return body;
        }

        private Stmt ifStatement()
        {
            consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
            Expr condition = expression();
            consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after if condition."); // [parens]

            Stmt thenBranch = statement();
            Stmt elseBranch = null;
            if (match(TokenType.TOKEN_ELSE))
            {
                elseBranch = statement();
            }

            return new Stmt.If(condition, thenBranch, elseBranch);
        }

        private Stmt printStatement()
        {
            Expr value = expression();
            consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after value.");
            return new Stmt.Print(value);
        }

        private Stmt returnStatement()
        {
            Token keyword = previous();
            Expr value = null;
            if (!check(TokenType.TOKEN_SEMICOLON))
            {
                value = expression();
            }
            consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after return value.");
            return new Stmt.Return(keyword, value);
        }

        private Stmt varDeclaration()
        {
            Token name = consume(TokenType.TOKEN_IDENTIFIER, "Expect variable name.");

            Expr initializer = null;
            if (match(TokenType.TOKEN_EQUAL))
            {
                initializer = expression();
            }

            consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after variable declaration.");
            return new Stmt.Var(name, initializer);
        }

        private Stmt whileStatement()
        {
            consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after while.");
            Expr condition = expression();
            consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after condition.");
            Stmt body = statement();

            return new Stmt.While(condition, body);
        }

        private Stmt expressionStatement()
        {
            Expr expr = expression();
            consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after expression.");
            return new Stmt.Expression(expr);
        }

        private Stmt.Function function(string kind)
        {
            Token name = consume(TokenType.TOKEN_IDENTIFIER, "Expect " + kind + " name.");
            consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after" + kind + " name.");
            List<Token> parameters = new List<Token>();
            if (!check(TokenType.TOKEN_RIGHT_PAREN))
            {
                do
                {
                    if (parameters.Count >= 255)
                    {
                        error(peek(), "Cannot have more than 255 parameters.");
                    }

                    parameters.Add(consume(TokenType.TOKEN_IDENTIFIER, "Expect parameter name."));
                }
                while (match(TokenType.TOKEN_COMMA));
            }
            consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
            consume(TokenType.TOKEN_LEFT_BRACE, "Expect '{' before " + kind + " body.");
            List<Stmt> body = block();
            return new Stmt.Function(name, parameters, body);
        }

        private List<Stmt> block()
        {
            List<Stmt> statements = new List<Stmt>();

            while (!check(TokenType.TOKEN_RIGHT_BRACE) && !isAtEnd())
            {
                statements.Add(declaration());
            }

            consume(TokenType.TOKEN_RIGHT_BRACE, "Expect '}' after block.");
            return statements;
        }

        private Expr finishCall(Expr callee)
        {
            List<Expr> arguments = new List<Expr>();
            if (!check(TokenType.TOKEN_RIGHT_PAREN))
            {
                do
                {
                    if (arguments.Count >= 255)
                    {
                        error(peek(), "Cannot have more than 255 arguments.");
                    }
                    arguments.Add(expression());
                }
                while (match(TokenType.TOKEN_COMMA));
            }

            Token paren = consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after arguments");
            return new Expr.Call(callee, paren, arguments);
        }

        private bool match(params TokenType[] types)
        {
            foreach (TokenType type in types)
            {
                if (check(type))
                {
                    advance();
                    return true;
                }
            }
            return false;
        }

        private Token consume(TokenType type, string message)
        {
            if (check(type))
                return advance();

            throw error(peek(), message);
        }

        private bool check(TokenType type)
        {
            if (isAtEnd())
                return false;
            return peek().type == type;
        }

        private Token advance()
        {
            if (!isAtEnd())
                current++;
            return previous();
        }

        private bool isAtEnd()
        {
            return peek().type == TokenType.TOKEN_EOF;
        }

        private Token peek()
        {
            return tokens[current];
        }

        private Token previous()
        {
            return tokens[current - 1];
        }

        private ParseError error(Token token, string message)
        {
            Lox_error(token, message); // work around
            return new ParseError();
        }

        static void Lox_error(Token token, string message) // work around
        {
            if (token.type == TokenType.TOKEN_EOF)
            {
                report(token.line, " at end", message);
            }
            else
            {
                report(token.line, " at '" + token.lexeme() + "'", message);
            }
        }

        private static void report(int line, string where, string message)
        {
            System.Console.WriteLine("[line " + line + "] Error" + where + ": " + message);
            hadError = true;
        }

        private void syncrhonize()
        {
            advance();

            while (!isAtEnd())
            {
                if (previous().type == TokenType.TOKEN_SEMICOLON)
                    return;

                switch (peek().type)
                {
                    case TokenType.TOKEN_CLASS:
                    case TokenType.TOKEN_FUN:
                    case TokenType.TOKEN_VAR:
                    case TokenType.TOKEN_FOR:
                    case TokenType.TOKEN_IF:
                    case TokenType.TOKEN_WHILE:
                    case TokenType.TOKEN_PRINT:
                    case TokenType.TOKEN_RETURN:
                        return;
                }

                advance();
            }
        }


        /*** PRATT EXPRESSION PARSER ***/

        enum Precedence
        {
            PREC_NONE,
            PREC_ASSIGNMENT,  // =        
            PREC_OR,          // or       
            PREC_AND,         // and      
            PREC_EQUALITY,    // == !=    
            PREC_COMPARISON,  // < > <= >=
            PREC_TERM,        // + -      
            PREC_FACTOR,      // * /      
            PREC_UNARY,       // ! -      
            PREC_CALL,        // . ()     
            PREC_PRIMARY
        }

        delegate Expr PrefixFn();
        delegate Expr InfixFn(Expr left);

        struct ParseRule
        {
            public PrefixFn prefix; // csharp delegate
            public InfixFn infix; // clox function pointer
            public Precedence precedence;

            public ParseRule(PrefixFn prefix, InfixFn infix, Precedence precedence)
            { this.prefix = prefix; this.infix = infix; this.precedence = precedence; }
        }

        private static ParseRule[] rules = {//
      new ParseRule(grouping, call,    Precedence.PREC_CALL),       // TOKEN_LEFT_PAREN      
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_RIGHT_PAREN     
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_LEFT_BRACE
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_RIGHT_BRACE     
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_COMMA           
      new ParseRule(null,     dot,    Precedence.PREC_CALL ),       // TOKEN_DOT             
      new ParseRule(unary,    binary,  Precedence.PREC_TERM ),       // TOKEN_MINUS           
      new ParseRule(null,     binary,  Precedence.PREC_TERM ),       // TOKEN_PLUS            
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_SEMICOLON       
      new ParseRule(null,     binary,  Precedence.PREC_FACTOR ),     // TOKEN_SLASH           
      new ParseRule(null,     binary,  Precedence.PREC_FACTOR ),     // TOKEN_STAR            
      new ParseRule(unary,     null,    Precedence.PREC_NONE ),       // TOKEN_BANG            
      new ParseRule(null,     binary,    Precedence.PREC_EQUALITY ),       // TOKEN_BANG_EQUAL      
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_EQUAL           
      new ParseRule(null,     binary,    Precedence.PREC_EQUALITY ),       // TOKEN_EQUAL_EQUAL     
      new ParseRule(null,     binary,    Precedence.PREC_COMPARISON ),       // TOKEN_GREATER         
      new ParseRule(null,     binary,    Precedence.PREC_COMPARISON ),       // TOKEN_GREATER_EQUAL   
      new ParseRule(null,     binary,    Precedence.PREC_COMPARISON ),       // TOKEN_LESS            
      new ParseRule(null,     binary,    Precedence.PREC_COMPARISON ),       // TOKEN_LESS_EQUAL      
      new ParseRule(variable,     null,    Precedence.PREC_NONE ),       // TOKEN_IDENTIFIER      
      new ParseRule(literal,     null,    Precedence.PREC_NONE ),       // TOKEN_STRING          
      new ParseRule(literal,   null,    Precedence.PREC_NONE ),       // TOKEN_NUMBER          
      new ParseRule(null,     logical,    Precedence.PREC_AND ),       // TOKEN_AND             
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_CLASS           
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_ELSE            
      new ParseRule(literal,     null,    Precedence.PREC_NONE ),       // TOKEN_FALSE           
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_FOR             
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_FUN             
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_IF              
      new ParseRule(literal,     null,    Precedence.PREC_NONE ),       // TOKEN_NIL             
      new ParseRule(null,     logical,    Precedence.PREC_OR ),       // TOKEN_OR
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_PRINT           
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_RETURN          
      new ParseRule(super_,     null,    Precedence.PREC_NONE ),       // TOKEN_SUPER           
      new ParseRule(this_,     null,    Precedence.PREC_NONE ),       // TOKEN_THIS            
      new ParseRule(literal,     null,    Precedence.PREC_NONE ),       // TOKEN_TRUE            
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_VAR             
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_WHILE           
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_ERROR           
      new ParseRule(null,     null,    Precedence.PREC_NONE ),       // TOKEN_EOF             
    };

        static Expr parsePrecedence(Precedence precedence)
        {
            Expr expr = null;
            parser.advance();
            PrefixFn prefixRule = getRule(parser.previous().type).prefix;
            if (prefixRule == null)
            {
                parser.error(parser.previous(), "Expect expression.");
                return null;
            }

            //bool canAssign = precedence <= Precedence.PREC_ASSIGNMENT;
            expr = prefixRule();

            while (precedence <= getRule(parser.peek().type).precedence)
            {
                parser.advance();
                InfixFn infixRule = getRule(parser.previous().type).infix;
                expr = infixRule(expr);
            }

            return expr;
        }

        static ParseRule getRule(TokenType type)
        {
            return rules[(int)type];
        }

        static Expr expression()
        {
            return parsePrecedence(Precedence.PREC_ASSIGNMENT);
        }

        static Expr call(Expr left)
        {
            Expr expr = null;
            expr = parser.finishCall(left);
            return expr;
        }

        static Expr grouping()
        {
            Expr expr = expression();
            parser.consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
            return new Expr.Grouping(expr);
        }

        static Expr dot(Expr left) // Get
        {
            Expr expr = null;

            Token name = parser.consume(TokenType.TOKEN_IDENTIFIER, "Expect property name after '.'.");

            if (parser.match(TokenType.TOKEN_EQUAL))
            {
                Expr value = expression();
                expr = new Expr.Set(left, name, value); // left is type variable
            }
            else
            {
                expr = new Expr.Get(left, name);
            }

            return expr;
        }

        static void invalidIfAssignment(Expr expr)
        {
            if (expr is Expr.Assign) // work around
            {
                parser.error(((Expr.Assign)expr).name, "Invalid assignment target.");
            }
        }

        static Expr unary()
        {
            Token operator_ = parser.previous();
            Expr right = parsePrecedence(Precedence.PREC_UNARY);
            invalidIfAssignment(right);
            return new Expr.Unary(operator_, right);
        }

        static Expr binary(Expr left)
        {
            Token operator_ = parser.previous();
            ParseRule rule = getRule(operator_.type);
            Expr right = parsePrecedence(rule.precedence + 1);
            invalidIfAssignment(right);
            return new Expr.Binary(left, operator_, right);
        }

        static Expr logical(Expr left) // very similar to binary
        {
            Token operator_ = parser.previous();
            ParseRule rule = getRule(operator_.type);
            Expr right = parsePrecedence(rule.precedence + 1);
            invalidIfAssignment(right);
            return new Expr.Logical(left, operator_, right);
        }

        static Expr variable()
        {

            Token name = parser.previous();
            if (parser.match(TokenType.TOKEN_EQUAL))
            {
                Expr value = parsePrecedence(Precedence.PREC_ASSIGNMENT);
                return new Expr.Assign(name, value);
            }
            return new Expr.Variable(parser.previous());
        }

        static Expr literal() // true/false/nil/string/number/
        {

            switch (parser.previous().type)
            {
                case TokenType.TOKEN_FALSE:
                    return new Expr.Literal(false);
                case TokenType.TOKEN_NIL:
                    return new Expr.Literal(null);
                case TokenType.TOKEN_TRUE:
                    return new Expr.Literal(true);
                case TokenType.TOKEN_STRING:
                    return new Expr.Literal(parser.previous().lexeme());
                case TokenType.TOKEN_NUMBER:
                    return new Expr.Literal(double.Parse(parser.previous().lexeme()));
                default:
                    return null; // Unreachable.
            }
        }

        static Expr super_()
        {
            Token keyword = parser.previous();
            parser.consume(TokenType.TOKEN_DOT, "Expect '.' after 'super'.");
            Token method = parser.consume(TokenType.TOKEN_IDENTIFIER, "Expect superclass method name.");
            return new Expr.Super(keyword, method);
        }

        static Expr this_()
        {
            return new Expr.This(parser.previous());
        }
    }
}
