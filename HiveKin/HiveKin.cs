using Modding;
using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SFCore.Utils;
using HKMirror;

namespace HiveKin
{
    public class HiveKinMod : Mod
    {
        private static HiveKinMod? _instance;

        internal static HiveKinMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(HiveKinMod)} was never constructed");
                }
                return _instance;
            }
        }

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public HiveKinMod() : base("HiveKin")
        {
            _instance = this;
        }

        public override void Initialize()
        {
            Log("Initializing");

            On.HutongGames.PlayMaker.Actions.PlayerDataBoolTest.OnEnter += OnPDBoolTestAction;
            On.HutongGames.PlayMaker.Actions.CallMethodProper.DoMethodCall += OnCallMethodProperAction;
            On.HutongGames.PlayMaker.Actions.BoolTest.OnEnter += OnBoolTestAction;
            On.HutongGames.PlayMaker.Actions.FloatAdd.OnEnter += OnFloatAddAction;
            On.HutongGames.PlayMaker.Actions.SetVelocity2d.OnEnter += OnSetVelocity2dAction;

            On.HeroController.CharmUpdate += OnCharmUpdate;
            On.HeroController.CanFocus += OnCanFocus;

            On.PlayMakerFSM.OnEnable += OnFSMEnable;

            ModHooks.AfterTakeDamageHook += AfterHCTakeDamageHook;

            Log("Initialized");
        }

        private int blockers = 0;
        private bool midPhase = false;

        //Spawn Hatchlings when casting Spells
        private void OnSetVelocity2dAction(On.HutongGames.PlayMaker.Actions.SetVelocity2d.orig_OnEnter orig, SetVelocity2d self)
        {
            orig(self);

            if (self.Fsm.GameObject.name == "Knight" && self.Fsm.Name == "Spell Control" && self.State.Name == "Spell End" && PlayerDataAccess.equippedCharm_29 && self.Fsm.PreviousActiveState.Name != "Send Event")
            {
                HeroController.instance.gameObject.transform.Find("Charm Effects").gameObject.LocateMyFSM("Hatchling Spawn").GetFsmAction<SpawnObjectFromGlobalPool>("Hatch", 2).gameObject.Value.gameObject.Spawn(HeroController.instance.gameObject.transform.position);

                if (self.Fsm.FsmComponent.GetFsmIntVariable("Spell Level").Value == 2)
                {
                    HeroController.instance.gameObject.transform.Find("Charm Effects").gameObject.LocateMyFSM("Hatchling Spawn").GetFsmAction<SpawnObjectFromGlobalPool>("Hatch", 2).gameObject.Value.gameObject.Spawn(HeroController.instance.gameObject.transform.position);
                }
            }
        }

        //Increase Regen Time
        private void OnFSMEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
        {
            orig(self);

            if (self.gameObject.name == "Health" && self.FsmName == "Hive Health Regen")
            {
                self.AddFsmTransition("Idle", "CONTINUE", "Start Recovery");
                self.AddFsmIntVariable("currentHP");
                self.AddFsmIntVariable("maxHP");

                self.AddFsmAction("Idle", new GetPlayerDataInt()
                {
                    gameObject = new FsmOwnerDefault()
                    {
                        OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                        GameObject = GameManager.instance.gameObject
                    },
                    intName = "health",
                    storeValue = self.GetFsmIntVariable("currentHP")
                });

                self.AddFsmAction("Idle", new GetPlayerDataInt()
                {
                    gameObject = new FsmOwnerDefault()
                    {
                        OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                        GameObject = GameManager.instance.gameObject
                    },
                    intName = "maxHealth",
                    storeValue = self.GetFsmIntVariable("maxHP")
                });

                self.AddFsmAction("Idle", new IntCompare()
                {
                    integer1 = self.GetFsmIntVariable("currentHP"),
                    integer2 = self.GetFsmIntVariable("maxHP"),
                    equal = null,
                    lessThan = FsmEvent.GetFsmEvent("CONTINUE"),
                    greaterThan = null,
                    everyFrame = false
                });
            }
        }

        //Hiveblood States
        private void OnFloatAddAction(On.HutongGames.PlayMaker.Actions.FloatAdd.orig_OnEnter orig, FloatAdd self)
        {
            orig(self);

            if (self.Fsm.GameObject.name == "Health" && self.Fsm.Name == "Hive Health Regen")
            {
                if (self.State.Name == "Recover 1")
                {
                    midPhase = false;
                }

                else if (self.State.Name == "Recover 2")
                {
                    midPhase = true;
                }

                else if (self.State.Name == "Reset Timer")
                {
                    self.Fsm.FsmComponent.ChangeFsmTransition("Reset Timer", "FINISHED", (midPhase && PlayerDataAccess.equippedCharm_28) ? "Recover 2" : "Recover 1");
                }
            }

        }
        private int AfterHCTakeDamageHook(int hazardType, int damageAmount)
        {
            //Baldur Shell activation
            if (hazardType == 1 && PlayerDataAccess.blockerHits > 0 && (damageAmount * (PlayerDataAccess.overcharmed ? 2 : 1) * ((BossSceneController.IsBossScene && BossSceneController.Instance.BossLevel == 1) ? 2 : 1)) >= PlayerDataAccess.health)
            {
                HeroController.instance.gameObject.transform.Find("Charm Effects/Blocker Shield").gameObject.LocateMyFSM("Control").SendEvent("FOCUS START");
                HeroController.instance.gameObject.transform.Find("Charm Effects/Blocker Shield").gameObject.LocateMyFSM("Control").SendEvent("BLOCKER HIT");
                return 0;
            }

            return damageAmount;
        }
        private void OnCharmUpdate(On.HeroController.orig_CharmUpdate orig, HeroController self)
        {
            orig(self);

            //Shape of Unn stuff
            midPhase = false;

            //Baldur Shell stuff
            blockers = PlayerDataAccess.blockerHits;

            //Quick Focus Regeneration Time
            GameCameras.instance.gameObject.transform.Find("HudCamera/Hud Canvas/Health").gameObject.LocateMyFSM("Hive Health Regen").GetFsmFloatVariable("Recover Time").Value = 5 - (PlayerDataAccess.equippedCharm_7 ? 1 : 0);
        }

        private void OnBoolTestAction(On.HutongGames.PlayMaker.Actions.BoolTest.orig_OnEnter orig, HutongGames.PlayMaker.Actions.BoolTest self)
        {
            //Always friendly bees
            if (self.State.Name == "Friendly?" && self.boolVariable.Name == "Friendly")
            {
                self.boolVariable.Value = true;
            }

            //Always broken honey pillars and walls
            else if (self.Fsm.GameObject.name.Contains("Hive Breakable Pillar") && self.Fsm.Name == "Pillar" && self.State.Name == "Init")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            else if (self.Fsm.GameObject.name == "Hive Break Wall" && self.Fsm.Name == "Smash" && self.State.Name == "Init")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            else if (self.Fsm.GameObject.name == "Break Floor 1" && self.Fsm.Name == "break_floor" && self.State.Name == "Initiate" && self.Fsm.GameObject.scene.name == "Hive_03_c")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            //Always available Bench
            else if (self.Fsm.GameObject.name == "Hive Bench" && self.Fsm.Name == "Control" && self.State.Name == "Init")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            //Always open entrances
            else if (self.Fsm.GameObject.name == "One Way Wall" && self.Fsm.Name == "break_floor" && self.State.Name == "Initiate" && self.Fsm.GameObject.scene.name == "Deepnest_East_01")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            else if (self.Fsm.GameObject.name == "Secret Mask" && self.Fsm.Name == "unmasker" && self.State.Name == "Idle" && self.Fsm.GameObject.scene.name == "Deepnest_East_01")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            else if (self.Fsm.GameObject.name == "Breakable Wall" && self.Fsm.Name == "breakable_wall_v2" && self.State.Name == "Activated?" && self.Fsm.GameObject.scene.name == "Abyss_03_c")
            {
                self.isFalse = FsmEvent.GetFsmEvent("ACTIVATE");
            }

            orig(self);
        }

        //Removes Focusing
        private bool OnCanFocus(On.HeroController.orig_CanFocus orig, HeroController self)
        {
            if (self.gameObject.LocateMyFSM("Spell Control").GetFsmBoolVariable("Dream Focus").Value)
            {
                return orig(self);
            }

            return false;
        }

        private void OnCallMethodProperAction(On.HutongGames.PlayMaker.Actions.CallMethodProper.orig_DoMethodCall orig, HutongGames.PlayMaker.Actions.CallMethodProper self)
        {
            orig(self);

            //Auto-regen heals fully
            if (self.Fsm.GameObject.name == "Health" && self.Fsm.Name == "Hive Health Regen" && self.State.Name == "Recover")
            {
                blockers = PlayerDataAccess.blockerHits;
                midPhase = false;
                PlayerDataAccess.blockerHits = blockers;

                //Spore Shroom + Defender's Crest clouds
                if (PlayerDataAccess.equippedCharm_17)
                {
                    if (PlayerDataAccess.equippedCharm_10)
                    {
                        HeroController.instance.gameObject.LocateMyFSM("Spell Control").GetFsmAction<SpawnObjectFromGlobalPool>("Dung Cloud", 0).gameObject.Value.gameObject.Spawn(HeroController.instance.gameObject.transform.position);
                    }

                    else
                    {
                        HeroController.instance.gameObject.LocateMyFSM("Spell Control").GetFsmAction<SpawnObjectFromGlobalPool>("Spore Cloud", 3).gameObject.Value.gameObject.Spawn(HeroController.instance.gameObject.transform.position);
                    }
                }
            }
        }
        private void OnPDBoolTestAction(On.HutongGames.PlayMaker.Actions.PlayerDataBoolTest.orig_OnEnter orig, HutongGames.PlayMaker.Actions.PlayerDataBoolTest self)
        {
            //Health always regens
            if (self.Fsm.GameObject.name == "Health" && self.Fsm.Name == "Hive Health Regen" && self.State.Name == "Check")
            {
                self.isFalse = FsmEvent.GetFsmEvent("HIVE");
            }

            //Health displays as Hive health
            else if (self.Fsm.GameObject.name.Contains("Health ") && self.Fsm.Name == "health_display" && self.State.Name == "Check Type")
            {
                self.isFalse = FsmEvent.GetFsmEvent("HIVE");
            }

            orig(self);
        }
    }
}
