using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using CounterStrikeSharp.API.Core.Attributes;

namespace ScoutsNKnives
{
    [MinimumApiVersion(53)]
    public class ScoutsNKnives : BasePlugin
    {
        public override string ModuleName => "Scouts N' Knives";
        public override string ModuleVersion => "0.0.1";
        public override string ModuleAuthor => "Fortis";
        public override string ModuleDescription => "Simple Scouts N' Knives Plugin";

        public static ConfigOptions? _configs = new();

        public override void Load(bool hotReload)
        {
            LoadConfigs();

            if (!_configs!.Enabled)
            {
                Console.WriteLine("[SnK] Is Not Enabled");
                return;
            }

            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Post);
            RegisterEventHandler<EventItemPickup>(OnItemPickedUp, HookMode.Post);
            RegisterEventHandler<EventItemPurchase>(OnItemPurchased, HookMode.Post);
            RegisterEventHandler<EventGameStart>(OnGameStart, HookMode.Post);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurtPre, HookMode.Pre);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            int autohop = _configs!.AutoHop ? 1 : 0;
            Server.ExecuteCommand("sv_maxvelocity 10000");
            Server.ExecuteCommand($"sv_autobunnyhopping {autohop}");
            Server.ExecuteCommand("sv_staminajumpcost 0");
            Server.ExecuteCommand("sv_staminalandcost 0");
            Server.ExecuteCommand("sv_staminamax 0");
            Server.ExecuteCommand("sv_staminarecoveryrate 0");
            Server.ExecuteCommand("sv_airaccelerate 7");
            Server.ExecuteCommand("sv_clamp_unsafe_velocities 0");
            Server.ExecuteCommand($"sv_gravity {_configs!.Gravity}");
            Server.ExecuteCommand("mp_weapons_max_gun_purchases_per_weapon_per_match -1");
            Server.ExecuteCommand($"mp_roundtime {_configs!.RoundTime}");
            return HookResult.Continue;
        }

        private HookResult OnGameStart(EventGameStart @event, GameEventInfo info)
        {
            if (!_configs!.Enabled) return HookResult.Continue;

            int autohop = _configs!.AutoHop ? 1 : 0;
            ConVar.Find("sv_maxvelocity")?.SetValue(10000);
            ConVar.Find("sv_autobunnyhopping")?.SetValue(autohop);
            ConVar.Find("sv_staminajumpcost")?.SetValue(0);
            ConVar.Find("sv_staminalandcost")?.SetValue(0);
            ConVar.Find("sv_staminamax")?.SetValue(0);
            ConVar.Find("sv_staminarecoveryrate")?.SetValue(0);
            ConVar.Find("sv_airaccelerate")?.SetValue(7);
            ConVar.Find("sv_clamp_unsafe_velocities")?.SetValue(0);
            ConVar.Find("sv_gravity")?.SetValue(_configs!.Gravity);
            ConVar.Find("mp_weapons_max_gun_purchases_per_weapon_per_match")?.SetValue(-1);
            ConVar.Find("mp_roundtime")?.SetValue(_configs!.RoundTime);
            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            if (!_configs!.Enabled) return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (!player.IsValid) return HookResult.Continue;
            if (player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

            SetupPlayer(player);
            player.PlayerPawn.Value.VelocityModifier = _configs!.Speed;
            player.PawnArmor = _configs!.Armor;

            return HookResult.Continue;
        }

        private HookResult OnItemPickedUp(EventItemPickup @event, GameEventInfo info)
        {
            if (!_configs!.Enabled) return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (!player.IsValid) return HookResult.Continue;
            if (player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

            if (@event.Defindex == WeaponData.AllowedWeapons.First().DefIndex) return HookResult.Continue;

            if (@event.Item == "knife") return HookResult.Continue;

            var weapon = WeaponData.BlockedWeapons.FirstOrDefault(x => x.DefIndex == @event.Defindex);

            SetupPlayer(player);
            player.PrintToChat($" {ChatColors.Red}[SnK] {ChatColors.White}Weapon {ChatColors.Purple}{weapon!.Name} {ChatColors.White} is not allowed");
            return HookResult.Continue;
        }

        private HookResult OnItemPurchased(EventItemPurchase @event, GameEventInfo info)
        {
            if (!_configs!.Enabled) return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (!player.IsValid) return HookResult.Continue;
            if (player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

            SetupPlayer(player);
            player.PrintToChat($" {ChatColors.Red}[SnK] {ChatColors.White}Purchasing weapons is disabled");
            return HookResult.Continue;
        }

        private HookResult OnPlayerHurtPre(EventPlayerHurt @event,  GameEventInfo info)
        {
            if (!_configs!.Enabled) return HookResult.Continue;

            if (@event.Weapon == WeaponData.AllowedWeapons.First().WeaponName)
            {
                @event.DmgArmor = (int)(@event.DmgArmor * _configs.Scout_Damage);
                @event.DmgHealth = (int)(@event.DmgHealth * _configs.Scout_Damage);
            }

            if (@event.Weapon == "weapon_knife")
            {
                @event.DmgArmor = (int)(@event.DmgArmor * _configs.Knife_Damage);
                @event.DmgHealth = (int)(@event.DmgArmor * _configs.Knife_Damage);
            }

            return HookResult.Continue;
        }

        private void SetupPlayer(CCSPlayerController player)
        {
            foreach (var weapon in player.PlayerPawn.Value.WeaponServices!.MyWeapons)
            {
                if (weapon.IsValid && weapon.Value.IsValid)
                {
                    weapon.Value.Remove();
                }
            }

            player.GiveNamedItem("weapon_ssg08");
            player.GiveNamedItem("weapon_knife");
        }

        private void LoadConfigs()
        {
            string path = Path.Join(ModuleDirectory, "scoutsnknives.json");
            _configs = !File.Exists(path) ? CreateConfig(path, LoadConfig) : JsonSerializer.Deserialize<ConfigOptions>(File.ReadAllText(path));
        }

        private static T CreateConfig<T>(string path, Func<T> dataLoader)
        {
            var data = dataLoader();
            File.WriteAllText(path, JsonSerializer.Serialize(data,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));

            return data;
        }

        private ConfigOptions LoadConfig()
        {
            return new ConfigOptions
            {
                Enabled = true,
                AutoHop = true,
                Armor = 100,
                Knife_Damage = 1.0,
                Scout_Damage = 1.0,
                Gravity = 450,
                RoundTime = 10,
                Speed = (float)1.0,
            };
        }

        public class ConfigOptions
        {
            public bool Enabled { get; init; }
            public bool AutoHop { get; init; }
            public int Armor { get; init; }
            public double Knife_Damage { get; init; }
            public double Scout_Damage { get; init; }
            public int Gravity { get; init; }
            public int RoundTime { get; init; }
            public float Speed { get; init; }
        }
    }
}