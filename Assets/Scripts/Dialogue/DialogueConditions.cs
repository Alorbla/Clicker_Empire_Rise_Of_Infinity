using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueConditions : MonoBehaviour
{
    private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> _numbers = new Dictionary<string, int>();

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _flags[key] = value;
    }

    public bool GetFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _flags.TryGetValue(key, out bool value) && value;
    }

    public void SetNumber(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _numbers[key] = value;
    }

    public int GetNumber(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        return _numbers.TryGetValue(key, out int value) ? value : 0;
    }

    public bool Evaluate(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return false;
        }

        string trimmed = expr.Trim();
        string[] ops = { ">=", "<=", "==", "!=", ">", "<" };
        for (int i = 0; i < ops.Length; i++)
        {
            string op = ops[i];
            int idx = trimmed.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0)
            {
                continue;
            }

            string left = trimmed.Substring(0, idx).Trim();
            string right = trimmed.Substring(idx + op.Length).Trim();
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            if (!int.TryParse(right, out int rhs))
            {
                return false;
            }

            int lhs = GetNumber(left);
            switch (op)
            {
                case ">=": return lhs >= rhs;
                case "<=": return lhs <= rhs;
                case "==": return lhs == rhs;
                case "!=": return lhs != rhs;
                case ">": return lhs > rhs;
                case "<": return lhs < rhs;
            }
        }

        return GetFlag(trimmed);
    }
}
