# 战斗资源来源

依据 ADR-13 从项目已包含的 Cowsins FPS Engine 复制原始模型和音频。授权沿用原资源包许可，仅供本项目使用，不作为独立资源分发。没有复制脚本、预制体、控制器或原 GUID；材质与动画控制器由本项目生成，禁止反向引用 ThirdParty。

|目标文件|来源（Assets/ThirdParty/Cowsins/）|SHA256|
|---|---|---|
|Rifle.fbx|Models/Weapons/Rifle_FPSEngineRig.fbx|94AF02D7959B87C462782104AD34E3D1BC8531CA2962554CA3FAB303DA9D8386|
|Launcher.fbx|Models/Weapons/RocketLauncher_FPSEngineRig.fbx|5591038D90870274E16D59AF5098A1C60C9472F3496FD00A12F16FA113EA0E21|
|RifleFire.wav|SFX/Weapons/Rifle/Rifle_Fire_SFX.wav|43D3148D4D512ADCAA600584EF763D421866F691B5B18D5AEDF0BAED4F26E6EE|
|RifleReload.wav|SFX/Weapons/Rifle/Rifle_Reload_SFX.wav|58EFF59299BD88DB8569C8E2AB185A794E51423682190771B097201CFC41EA1B|
|LauncherFire.wav|SFX/Weapons/RocketLauncher/RocketLauncher_Fire_SFX.wav|CA7C4C2D76260AC9FBA2C604DECCC56F72DA86D4646DFA9CB4F32D88072DF023|
|LauncherReload.wav|SFX/Weapons/RocketLauncher/RocketLauncher_Reload_SFX.wav|70BFEC66E67DA47D83F950B1900D39BC78A3EA1A198295EFFF603B6180CF1D54|
|Hit.wav|SFX/Others/hitmarker_SFX.wav|5F065EC15D5694990B4FDBDC7510576D85DCFA3C1E2E7D1D2E5B2825D2D5FE03|
|Explosion.wav|SFX/Others/LargeExplosion.wav|BAD69C842A3DD4BFA476BCDAD3D719ED13FC7082F310068898F302FF45580DA9|

使用 AFPS/Combat/Rebuild Presentation Assets 菜单配置 Generic 动画、清除动画事件并重建自有控制器和材质。枪模姿态由表现层归一化，不参与命中或伤害。

模型原始出处说明见 CombatModelLicense.txt；部分模型由 Cowsins 修改自 Tasty Tony 的 CC Attribution 模型，作者：https://sketchfab.com/TastyTony 。保留作者署名及修改说明。

基础位置化脚步使用以下原始 WAV（当前不区分地表材质）：

|目标文件|来源（Assets/ThirdParty/Cowsins/）|SHA256|
|---|---|---|
|Footstep1.wav|SFX/Footsteps/wood_Footstep01.wav|8499BB970F214C3B6B5D8C96DC5C76613722F1D4DCCB42A87E9DE316B6E6A46B|
|Footstep2.wav|SFX/Footsteps/wood_Footstep02.wav|B682197C4A72202D7815B21694AFF161E0CC27A65E20A3222D4A266831C2F97D|
