using System;
using Celeste.Mod;
using Monocle;

namespace Celeste.Mod.KevinCrusher {
    public class KevinCrusherModule : EverestModule {
        public static KevinCrusherModule Instance { get; private set; }

        public KevinCrusherModule() {
            Instance = this;
        }

        public override void Load() {
            Logger.Log(LogLevel.Info, "KevinCrusher", "KevinCrusher Module Loaded successfully!");
        }

        public override void Unload() {
        }
    }
}