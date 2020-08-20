#define NAN_BOXING
#if NAN_BOXING
using Value_t = System.UInt64;
#else
using clox;
#endif

namespace util
{
    /*
     * Ad hoc memory manager
     * fake pseudo pointers
     */
    class cMem<T>
    {
        public T[] contents;
        private System.Collections.BitArray isVacant;
        private int count;
        private int capacity = 16;// preallocate

        // vacancy management
        private const int VACANT_MAX = 128;
        private int[] vacantSlots;
        private int vacantTop;
        private bool isFragmented;
        private bool isTooFragmented;

        public cMem()
        {
            contents = new T[capacity];
            isVacant = new System.Collections.BitArray(capacity);
            isVacant.SetAll(true);
            isVacant[0] = false;
            count = 1;

            // vacancy management        
            vacantSlots = new int[VACANT_MAX];
            vacantTop = 0;
            isFragmented = false;
            isTooFragmented = false;
        }

        public int store(T entry)
        {
            count++;
            if (count >= capacity)
            {
                int oldCapacity = capacity;
                capacity = clox.Memory.GROW_CAPACITY(oldCapacity);
                clox.Memory.reallocate<T>(ref contents, oldCapacity, capacity);
                isVacant.Length = capacity;

                for (int i = count; i < capacity; i++)
                {
                    isVacant[i] = true;
                }

                isFragmented = false;
                isTooFragmented = false;
            }

            int index = count - 1;
            if (isFragmented)
            {
                if (vacantTop > 0)
                {
                    index = vacantSlots[vacantTop - 1];
                    vacantTop--;
                    if (vacantTop == 0 && !isTooFragmented)
                        isFragmented = false;
                }
                else
                {
                    for (int i = capacity - 1; i >= 0; --i)
                    {
                        if (isVacant[i])
                        {
                            index = i;
                            break;
                        }
                    }
                }
            }

            contents[index] = entry;
            isVacant[index] = false;
            return index; // The fake "pointer"
        }

        public T get(int index)
        {
            return contents[index];
        }

        public void remove(int index)
        {
            if (index == 0) // first is reserved (or null?)
                return;

            isVacant[index] = true;
            count--;

            if (count == 1)
            {
                isFragmented = false;
                isTooFragmented = false;
                return;
            }

            if (index != count)
                isFragmented = true;

            if (isFragmented && !isTooFragmented)
            {
                vacantSlots[vacantTop++] = index;
            }

            if (vacantTop == VACANT_MAX)
                isTooFragmented = true;
        }

        public void free()
        {
            clox.Memory.FREE_ARRAY<T>(typeof(T), ref contents, count);
            isVacant.Length = 0;
            contents = null;
            isVacant = null;
        }
    }

    static class cHeap
    {
        public static cMem<clox.Obj> objects = new cMem<clox.Obj>();
        public static cMem<Value_t> values = new cMem<Value_t>();
    }

    static class util
    {
        public static bool _memcmp(char[] source, int start, string rest, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (source[i + start] != rest[i])
                    return false;
            }
            return true;
        }

        public static bool _memcmp(char[] source1, int start1, char[] source2, int start2, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (source1[i + start1] != source2[i + start2])
                    return false;
            }
            return true;
        }

        // used by concatenate
        public static void _memcpy<T>(T[] dest, int destOffset, T[] src, int srcOffset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                dest[destOffset + i] = src[srcOffset + i];
            }
        }

        // used by concatenate
        public static void _memcpy<T>(T[] dest, T[] src, int srcOffset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                dest[i] = src[srcOffset + i];
            }
        }

    }

    class flag
    {

        public static lox.InterpreterType InterpreterType;
        public static lox.RunType RunType;
        public static bool EngineIsSpecified;
        public static int ArgsFileIndex;

        /*
         * Quick and dirty non-general function
         * for parsing command line arguments
         */
        public static void Parse(string[] args)
        {
            // defaults
            InterpreterType = lox.InterpreterType.CLOX_BYTECODE_VM;
            RunType = lox.RunType.RUN_REPL;
            EngineIsSpecified = false;
            ArgsFileIndex = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string s = args[i];
                if (s[0] == '-')
                {
                    if (s.Length > 1 && s[1] == 'j')
                        InterpreterType = lox.InterpreterType.JLOX_INTERPRETER;

                    EngineIsSpecified = true;
                }
                else
                {
                    ArgsFileIndex = i;
                }
            }

            if (EngineIsSpecified && args.Length > 1)
                RunType = lox.RunType.RUN_FILE;

            if (!EngineIsSpecified && args.Length > 0)
                RunType = lox.RunType.RUN_FILE;
        }
    }
}
