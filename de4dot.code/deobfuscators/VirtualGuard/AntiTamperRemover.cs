using de4dot.blocks;
using dnlib.DotNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de4dot.code.deobfuscators.VirtualGuard
{
    internal class AntiTamperRemover
    {
        private ModuleDefMD module;
        private MethodDef antiTamperMethod;
        private List<string> HashCheckMethods = new List<string>() {
                                        "System.Reflection.Assembly System.Reflection.Assembly::GetExecutingAssembly()",
                                        "System.String System.Reflection.Assembly::get_Location()",
                                        "System.Security.Cryptography.MD5 System.Security.Cryptography.MD5::Create()",
                                        "System.Byte[] System.IO.File::ReadAllBytes(System.String)",
                                        "System.Byte[] System.Security.Cryptography.HashAlgorithm::ComputeHash(System.Byte[])"
                                        };
        public AntiTamperRemover(ModuleDefMD module)
        {
            this.module = module;
        }

        public MethodDef Method
        {
            get { return antiTamperMethod; }
        }

        public bool Find(MethodDef realCctor)
        {
            if (realCctor == null)
                return false;
            foreach (var method in DotNetUtils.GetCalledMethods(module, realCctor))
            {
                int i = 0;
                var allCalls = DotNetUtils.GetMethodCalls(method);
                IEnumerable<string> allCallsNames = allCalls.Select(pt => pt.FullName);
                foreach (var calledMethodName in allCallsNames)
                {
                    if (calledMethodName == HashCheckMethods[i])
                        i++;
                    if (i == HashCheckMethods.Count)
                    {
                        antiTamperMethod = method;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
