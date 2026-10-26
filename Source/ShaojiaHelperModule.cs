using System;

namespace Celeste.Mod.ShaojiaHelper {
    public class ShaojiaHelperModule : EverestModule {
        public static ShaojiaHelperModule Instance { get; private set; } = null!;

        public override Type SettingsType => typeof(ShaojiaHelperModuleSettings);
        public static ShaojiaHelperModuleSettings Settings => (ShaojiaHelperModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(ShaojiaHelperModuleSession);
        public static ShaojiaHelperModuleSession Session => (ShaojiaHelperModuleSession)Instance._Session;

        public override Type SaveDataType => typeof(ShaojiaHelperModuleSaveData);
        public static ShaojiaHelperModuleSaveData SaveData => (ShaojiaHelperModuleSaveData)Instance._SaveData;

        public ShaojiaHelperModule() {
            Instance = this;
        }

        public override void Load() {
        }

        public override void Unload() {
        }
    }
}