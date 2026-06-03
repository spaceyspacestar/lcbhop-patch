using BepInEx.Configuration;

namespace lcbhop {
    public class Config {
        private readonly ConfigFile config;

        public bool autobhop { get; set; }
        public bool speedometer { get; set; }
        public bool enablebunnyhopping { get; set; }

        // MoveVars
        public float gravity { get; set; }
        public float friction { get; set; }
        public float maxspeed { get; set; }
        public float movespeed { get; set; }
        public float accelerate { get; set; }
        public float airaccelerate { get; set; }
        public float stopspeed { get; set; }

        public bool falldmg { get; set; }
        public bool staminadrain { get; set; }
        public float walkspeed { get; set; }


        public Config( ConfigFile cfg ) {
            config = cfg;
        }

        public void Init( ) {
            autobhop = config.Bind( "General", "Auto Bhop", true, "Disabling rebinds jump to scroll, needs ItemQuickSwitch mod!" ).Value;
            speedometer = config.Bind( "General", "Speedometer", false, "Enables speedometer HUD." ).Value;
            enablebunnyhopping = config.Bind( "General", "Enable bunnyhopping", false, "Disables the speed cap." ).Value;
            
            falldmg = config.Bind( "General", "Enable fall damage", true, "Enables fall damage." ).Value;
            staminadrain = config.Bind( "General", "Enables stamina drain", true, "Enables stamina drain." ).Value;

            gravity = config.Bind( "Move Vars", "Gravity", 800.0f, "Gravity." ).Value;
            friction = config.Bind( "Move Vars", "Friction", 4.0f, "Ground friction." ).Value;
            maxspeed = config.Bind( "Move Vars", "Max Speed", 320.0f, "Max speed per tick." ).Value;
            movespeed = config.Bind( "Move Vars", "Move Speed", 300.0f, "Ground Sprint speed (like cl_forwardspeed etc.)." ).Value;
            walkspeed = config.Bind( "Move Vars", "Walking Speed", 150.0f, "Ground Walk speed (like cl_forwardspeed etc.)." ).Value;

            accelerate = config.Bind( "Move Vars", "Accelerate", 5.0f, "Ground acceleration." ).Value;
            airaccelerate = config.Bind( "Move Vars", "Air Accelerate", 10.0f, "Air acceleration." ).Value;
            stopspeed = config.Bind( "Move Vars", "Stop Speed", 75.0f, "Ground deceleration." ).Value;

        }
    }
}