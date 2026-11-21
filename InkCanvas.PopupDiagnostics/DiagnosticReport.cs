using System;
using System.Collections.Generic;

namespace Ink_Canvas.PopupDiagnostics
{
    /// <summary>
    /// 诊断结果收集器：逐条记录"检查了什么、是否符合预期、实测值是多少"。
    /// </summary>
    /// <remarks>
    /// 诊断工具的价值全在"失败时能看懂哪里不对"，所以每条结果都带上实测值，
    /// 而不是只打印一个 PASS/FAIL。
    /// </remarks>
    internal sealed class DiagnosticReport
    {
        private sealed class Entry
        {
            public string Name;
            public bool Passed;
            public string Detail;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>进度日志（不属于检查项，单纯说明当前在做什么）。</summary>
        public void Note(string message)
        {
            Console.WriteLine("  · " + message);
        }

        /// <summary>记录一条检查结果。</summary>
        public void Check(string name, bool passed, string detail = null)
        {
            _entries.Add(new Entry { Name = name, Passed = passed, Detail = detail });
            Console.WriteLine((passed ? "  PASS  " : "  FAIL  ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : "  | " + detail));
        }

        /// <summary>记录一条无条件失败（例如场景抛异常）。</summary>
        public void Fail(string name, string detail)
        {
            Check(name, false, detail);
        }

        /// <summary>全部检查是否通过。</summary>
        public bool AllPassed
        {
            get
            {
                foreach (var entry in _entries)
                {
                    if (!entry.Passed) return false;
                }
                return true;
            }
        }

        /// <summary>打印汇总行，返回退出码（0 = 全通过）。</summary>
        public int Summarize(string title)
        {
            int passed = 0;
            foreach (var entry in _entries)
            {
                if (entry.Passed) passed++;
            }

            Console.WriteLine();
            Console.WriteLine("【" + title + "】" + passed + "/" + _entries.Count + (AllPassed ? " 全部通过" : " 存在失败"));
            return AllPassed ? 0 : 1;
        }
    }
}
