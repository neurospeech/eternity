﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace NeuroSpeech.Eternity
{
    internal static class ILWriterExtensions
    {
        public static void EmitLoadArg(this ILGenerator il, int index)
        {
            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldarg_0);
                    return;
                case 1:
                    il.Emit(OpCodes.Ldarg_1);
                    return;
                case 2:
                    il.Emit(OpCodes.Ldarg_2);
                    return;
                case 3:
                    il.Emit(OpCodes.Ldarg_3);
                    return;
            }
            if (index <= 255)
            {
                il.Emit(OpCodes.Ldarg_S, (byte)index);
                return;
            }
            il.Emit(OpCodes.Ldarg, index);

        }

        public static void EmitConstant(this ILGenerator il, int i)
        {
            switch (i)
            {
                case -1:
                    il.Emit(OpCodes.Ldc_I4_M1);
                    return;
                case 0:
                    il.Emit(OpCodes.Ldc_I4_0);
                    return;
                case 1:
                    il.Emit(OpCodes.Ldc_I4_1);
                    return;
                case 2:
                    il.Emit(OpCodes.Ldc_I4_2);
                    return;
                case 3:
                    il.Emit(OpCodes.Ldc_I4_3);
                    return;
                case 4:
                    il.Emit(OpCodes.Ldc_I4_4);
                    return;
                case 5:
                    il.Emit(OpCodes.Ldc_I4_5);
                    return;
                case 6:
                    il.Emit(OpCodes.Ldc_I4_6);
                    return;
                case 7:
                    il.Emit(OpCodes.Ldc_I4_7);
                    return;
                case 8:
                    il.Emit(OpCodes.Ldc_I4_8);
                    return;
            }
            //if (sbyte.MinValue > i && i < sbyte.MaxValue) {
            //    il.Emit(OpCodes.Ldc_I4_S, (sbyte)i);
            //    return;
            //}
            il.Emit(OpCodes.Ldc_I4, i);
            return;
        }
    }
}
