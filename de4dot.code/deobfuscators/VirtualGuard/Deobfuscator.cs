/*
    Copyright (C) 2011-2017 TheProxy

    This file is part of modified de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using de4dot.blocks;
using de4dot.blocks.cflow;
using de4dot.code.renamer.asmmodules;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace de4dot.code.deobfuscators.VirtualGuard
{
    public class DeobfuscatorInfo : DeobfuscatorInfoBase
    {
        internal const string THE_NAME = "VirtualGuard";
        public const string THE_TYPE = "vg";
        private const string DEFAULT_REGEX = DeobfuscatorBase.DEFAULT_ASIAN_VALID_NAME_REGEX;
        private BoolOption removeAntiDebugProtection;
        private BoolOption removeAntiTamperProtection;

        public DeobfuscatorInfo()
            : base(DEFAULT_REGEX)
        {
            removeAntiDebugProtection = new BoolOption(null, MakeArgName("ad"), "Remove anti-debug protection code", true);
            removeAntiTamperProtection = new BoolOption(null, MakeArgName("at"), "Remove anti-tamper protection code", true);
        }

        public override string Name => THE_NAME;
        public override string Type => THE_TYPE;

        public override IDeobfuscator CreateDeobfuscator()
        {
            return new Deobfuscator(new Deobfuscator.Options
            {
                RenameResourcesInCode = false,
                ValidNameRegex = validNameRegex.Get(),
                RemoveAntiDebugProtection = removeAntiDebugProtection.Get(),
                RemoveAntiTamperProtection = removeAntiTamperProtection.Get(),
            });
        }
        protected override IEnumerable<Option> GetOptionsInternal()
        {
            return new List<Option>() {
                removeAntiDebugProtection,
                removeAntiTamperProtection,
            };
        }

        private class Deobfuscator : DeobfuscatorBase
        {
            Options options;
            private readonly ControlFlowFixer _controlFlowFixer = new ControlFlowFixer();
            private bool _detectedVirtualGuard;
            private string _version = "";
            ProxyCallFixer proxyCallFixer;
            AntiDebugRemover antiDebugRemover;
            AntiTamperRemover antiTamperRemover;

            public Deobfuscator(Options options)
                : base(options)
            {
                this.options = options;
            }

            public override string Type => THE_TYPE;
            public override string TypeLong => THE_NAME;
            public override string Name => $"{TypeLong} {_version}";

            public override MetadataFlags MetadataFlags => MetadataFlags.PreserveAll;

            public override IEnumerable<IBlocksDeobfuscator> BlocksDeobfuscators
            {
                get
                {
                    return new List<IBlocksDeobfuscator>
                    {
                        _controlFlowFixer
                    }; 
                }
            }

            protected override int DetectInternal()
            {
                var val = 0;
                if (_detectedVirtualGuard) val += 2;
                return val;
            }

            protected override void ScanForObfuscator()
            {
                DetectVirtualGuard();
                proxyCallFixer = new ProxyCallFixer(module);
            }

            private void DetectVirtualGuard()
            {
                List<string> VMNames = new List<string>() { "crocodile", "spider"};
                if (module.Name != "𝙑𝙞𝙧𝙩𝙪𝙖𝙡𝙂𝙪𝙖𝙧𝙙")
                    return;
                foreach (var res in module.Resources)
                {
                    if(VMNames.Contains(res.Name))
                        _detectedVirtualGuard = true;
                }
            }

            public override void DeobfuscateBegin()
            {
                base.DeobfuscateBegin();
            }

            public override void DeobfuscateEnd()
            {
                var cctor = DotNetUtils.GetModuleTypeCctor(module);
                MethodDef realCctor = DotNetUtils.GetCalledMethods(module, cctor).First();
                
                //proxy calls are now removed so its easier to detect now!
                if(options.RemoveAntiDebugProtection)
                {
                    antiDebugRemover = new AntiDebugRemover(module);
                    if (antiDebugRemover.Find(realCctor))
                    {
                        Logger.n("Removing calls to Anti Debug Method: " + antiDebugRemover.Method);
                        Utils.RemoveCall(realCctor, antiDebugRemover.Method);
                    }
                }

                if(options.RemoveAntiTamperProtection)
                {
                    antiTamperRemover = new AntiTamperRemover(module);
                    if (antiTamperRemover.Find(realCctor))
                    {
                        Logger.n("Removing calls to Anti Tamper Method: " + antiTamperRemover.Method);
                        Utils.RemoveCall(realCctor, antiTamperRemover.Method);
                    }
                }

                FindAndRemoveInlinedMethods();

                //TODO: Might not always be correct
                //No more mixed!
                module.IsILOnly = true;
                base.DeobfuscateEnd();
            }

            public override IEnumerable<int> GetStringDecrypterMethods()
            {
                return new List<int>();
            }
            
            public override void DeobfuscateMethodEnd(Blocks blocks)
            {
                List<TypeDef> proxyTypes = proxyCallFixer.Deobfuscate(blocks);
                AddTypesToBeRemoved(proxyTypes, "proxies");
                base.DeobfuscateMethodEnd(blocks);
            }

            internal class Options : OptionsBase
            {
                public bool RemoveAntiDebugProtection { get; set; }
                public bool RemoveAntiTamperProtection { get; set; }

            }
        }
    }
}
