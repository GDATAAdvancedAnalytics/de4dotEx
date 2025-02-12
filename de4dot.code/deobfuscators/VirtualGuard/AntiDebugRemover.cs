using de4dot.blocks;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de4dot.code.deobfuscators.VirtualGuard
{
    internal class AntiDebugRemover
    {
        private ModuleDefMD module;
        private MethodDef antiDebuggerMethod;

        public AntiDebugRemover(ModuleDefMD module)
        {
            this.module = module;
        }

        public MethodDef Method
        {
            get { return antiDebuggerMethod; }
        }

        public bool Find(MethodDef realCctor)
        {
            if (realCctor == null)
                return false;
            foreach (var method in DotNetUtils.GetCalledMethods(module, realCctor))
            {
                var type = method.DeclaringType;
                if (!method.IsStatic || !DotNetUtils.IsMethod(method, "System.Void", "()"))
                    continue;
                if (DotNetUtils.GetPInvokeMethod(type, "kernel32", "LoadLibrary") == null)
                    continue;
                if (DotNetUtils.GetPInvokeMethod(type, "kernel32", "GetProcAddress") == null)
                    continue;
                if (!DotNetUtils.HasString(method, "IsDebuggerPresent") &&
                    !DotNetUtils.HasString(method, "CheckRemoteDebuggerPresent") &&
                    !DotNetUtils.HasString(method, "get_IsAttached"))
                    continue;

                antiDebuggerMethod = method;
                return true;
            }

            return false;
        }
    }
}
