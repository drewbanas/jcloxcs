namespace lox
{
    public enum TokenType
    {
        // Single-character tokens.                         
        TOKEN_LEFT_PAREN, TOKEN_RIGHT_PAREN,
        TOKEN_LEFT_BRACE, TOKEN_RIGHT_BRACE,
        TOKEN_COMMA, TOKEN_DOT, TOKEN_MINUS, TOKEN_PLUS,
        TOKEN_SEMICOLON, TOKEN_SLASH, TOKEN_STAR,

        // One or two character tokens.                     
        TOKEN_BANG, TOKEN_BANG_EQUAL,
        TOKEN_EQUAL, TOKEN_EQUAL_EQUAL,
        TOKEN_GREATER, TOKEN_GREATER_EQUAL,
        TOKEN_LESS, TOKEN_LESS_EQUAL,

        // Literals.                                        
        TOKEN_IDENTIFIER, TOKEN_STRING, TOKEN_NUMBER,

        // Keywords.                                        
        TOKEN_AND, TOKEN_CLASS, TOKEN_ELSE, TOKEN_FALSE,
        TOKEN_FOR, TOKEN_FUN, TOKEN_IF, TOKEN_NIL, TOKEN_OR,
        TOKEN_PRINT, TOKEN_RETURN, TOKEN_SUPER, TOKEN_THIS,
        TOKEN_TRUE, TOKEN_VAR, TOKEN_WHILE,

        TOKEN_ERROR,
        TOKEN_EOF
    }

    public struct Token
    {
        public TokenType type;
        public int start; // start and length replaces lexeme in jlox
        public int length;
        public int line;
        public char[] _char_ptr; // points to the original source code string

        public string lexeme() // work around for jlox parser
        {
            if (type == TokenType.TOKEN_STRING)
                return new string(_char_ptr, start + 1, length - 2); // trim quotation marks
            else
                return new string(_char_ptr, start, length); // clox handles the quotation marks
        }
    }
}
