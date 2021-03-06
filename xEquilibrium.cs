/*  Copyright 2012 aether

    This plugin file is part of BF3 PRoCon.

    BFBC2 PRoCon is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BF3 PRoCon is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BF3 PRoCon.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using System.Threading;
using System.Windows.Forms;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Maps;

namespace PRoConEvents 
{
    public class xEquilibrium : PRoConPluginAPI, IPRoConPluginInterface 
    {
        #region Variables and Constructors
        
        // ======================
        // Plugin Variables
        // ======================

        private string m_strHostName;
        private string m_strPort;
        private string m_strPRoConVersion;

        //======================
        // Enums
        //======================
        enum State
        {
            Enabled,
            Disabled
        }
        enum RState
        {
            Allowed,
            Restricted,
            Disallowed
        }
        public enum KickCondition
        {
            LastJoin
        }

        //======================
        // UI Settings
        //======================
        private State enableJoinBalancer;
        private enumBoolYesNo alwaysJoinFriends;

        private State enableLiveBalancer;
        private enumBoolYesNo liveKeepSquadsTogether;
        private enumBoolYesNo showLiveAnnouncement;
        /// <summary>String value describing the agressiveness of the live balancing algorithm, ranging from 'Passive' to 'Brutal'.</summary>
        private string liveAggressiveness;                             
        private RState allowTeamChanges;    
                    
        private State enableRoundBalancer;
        private enumBoolYesNo roundKeepSquadsTogether;
        private State enableMapBias;

        private string friendsGrouping;

        private State enableWhitelist;
        private enumBoolYesNo includeReserved;
        private enumBoolYesNo includeAccounts;
        private List<string> liveWhitelist;

        private int globalDebugLevel;
        private enumBoolYesNo showPlayerJoins;
        private enumBoolYesNo showTeamChanges;
        private enumBoolYesNo showSquadChanges;
        private enumBoolYesNo showLiveMonitor;
        private enumBoolYesNo showTeamBalances;
        private enumBoolYesNo showFailedCommands;

        /// <summary>Is the current user an expert user?</summary>
        private enumBoolYesNo isExpert;

        /// <summary>When the server is set to Unranked mode (usually for scrims), disable the balancing functions.</summary>
        private enumBoolYesNo disableInUnranked;

        /// <summary>Maximum difference between the two teams player counts before action will be taken.</summary>                          
        private int futurePlayerCountDiffWaitThreshold;

        /// <summary>Maximum difference between the two teams player counts before action will be taken.</summary>                          
        private int futurePlayerCountDiffKillThreshold;

        /// <summary>Maximum difference between the two teams player counts before action will be taken.</summary>                          
        private int currentPlayerCountDiffWaitThreshold;

        /// <summary>Maximum difference between the two teams player counts before action will be taken.</summary>                          
        private int currentPlayerCountDiffKillThreshold;

        /// <summary>Maximum difference between the two teams dexterity before action will be taken.</summary>           
        private int futureDexDiffWaitThreshold;

        /// <summary>Maximum difference between the two teams dexterity before action will be taken.</summary>           
        private int futureDexDiffKillThreshold;

        /// <summary>Maximum difference between the two teams dexterity before action will be taken.</summary>           
        private int currentDexDiffWaitThreshold;

        /// <summary>Maximum difference between the two teams dexterity before action will be taken.</summary>           
        private int currentDexDiffKillThreshold;

        /// <summary>A value to determine how much more dexterity the losing team should get based on the score difference.</summary>             
        private double winningSkillComp;

        /// <summary>Threshold when the live balancer stops taking action.</summary>                
        private int disableTicketThres;

        /// <summary>Maximum number of forced team changes to a player while in the server.</summary>                
        private int playerMoveLimit;

        /// <summary>Interval in seconds between the livebalance checks.</summary>
        private int updateInterval;

        /// <summary>Delay, in seconds, between the end of round and when the round balancer starts.</summary>                  
        private int roundTimeDelay;

        /// <summary>Minimum friend cluster size to split into smaller clusters.</summary>                 
        private int splitFriendsThreshold;                      

        //======================
        // Other Variables
        //======================

        private bool shownSquadTogetherWarning = false;

        private string serverName = "Undefined";
        private Dictionary<string, string> otherServerNames = new Dictionary<string, string>();

        private Object teamChangesLock = new Object();

        /// <summary>Has the nextmap been shown this round?</summary>                  
        private bool nextMapShown = false;

        /// <summary>Duration to turn the Dice autobalnce off for from the start of the round.</summary>
        private static int antiAutobalanceDuration = 10;        
             
        private int alternateTeams = 0;
        
        private DateTime roundStart = DateTime.Now;
        private DateTime lastDelta = DateTime.Now;
        /// <summary>The time when to turn the Dice autobalance back on.</summary>   
        private DateTime autobalanceExpire = DateTime.Now;

        /// <summary>Class containing recent team changes</summary>
        private List<TeamChange> teamChanges = new List<TeamChange>();

        /// <summary>List of player that can be moved to balance the teams. Waiting for their death.</summary>
        private List<xPlayer> playersToMove = new List<xPlayer>();
        private List<string> reservedPlayers = new List<string>();

        //======================
        // Live Balance
        //======================
        enum BalanceState
        {
            Balanced,
            Stacked
        }
        private BalanceState previousBState = BalanceState.Stacked;

        private List<string> roundMap = new List<string>();
        private List<List<int>> roundScores = new List<List<int>>();
        private List<List<int>> roundPlayerCount = new List<List<int>>();
        private List<List<int>> roundDexterity = new List<List<int>>();

        //======================
        // Server Data
        //======================

        private xServer server = null;
        private static readonly object serverLock = new object();
		   
        public xEquilibrium() 
        {
            this.enableJoinBalancer = State.Enabled;
            this.alwaysJoinFriends = enumBoolYesNo.No;

            this.enableLiveBalancer = State.Enabled;
            this.liveKeepSquadsTogether = enumBoolYesNo.No;
            this.showLiveAnnouncement = enumBoolYesNo.No;
            this.liveAggressiveness = "Moderate";
            this.allowTeamChanges = RState.Restricted;

            this.enableRoundBalancer = State.Enabled;
            this.roundKeepSquadsTogether = enumBoolYesNo.No;
            this.enableMapBias = State.Enabled;

            this.friendsGrouping = "Fewest Friends";         
            
            this.enableWhitelist = State.Disabled;
            this.includeReserved = enumBoolYesNo.No;
            this.includeAccounts = enumBoolYesNo.No;
            this.liveWhitelist = new List<string>();

            this.globalDebugLevel = 3;
            this.showPlayerJoins = enumBoolYesNo.Yes;
            this.showTeamChanges = enumBoolYesNo.Yes;
            this.showSquadChanges = enumBoolYesNo.Yes;
            this.showLiveMonitor = enumBoolYesNo.Yes;
            this.showTeamBalances = enumBoolYesNo.Yes;
            this.showFailedCommands = enumBoolYesNo.No;

            this.isExpert = enumBoolYesNo.No;
            this.disableInUnranked = enumBoolYesNo.No;
            this.futurePlayerCountDiffWaitThreshold = 2;
            this.currentPlayerCountDiffWaitThreshold = 4;
            this.futurePlayerCountDiffKillThreshold = 4;
            this.currentPlayerCountDiffKillThreshold = 5;
            this.futureDexDiffWaitThreshold = 1000;
            this.currentDexDiffWaitThreshold = 1500;
            this.futureDexDiffKillThreshold = 3000;
            this.currentDexDiffKillThreshold = 4000;
            this.winningSkillComp = 10;
            this.disableTicketThres = 15;
            this.playerMoveLimit = 1;
            this.updateInterval = 5;
            this.roundTimeDelay = 40;
            this.splitFriendsThreshold = 8;           
        }

        #endregion

        #region PluginSetup

        public string GetPluginName() 
        {
            return "xEquilibrium";
        }

        public string GetPluginVersion() {
            return "1.0.0.0";
        }

        public string GetPluginAuthor() {
            return "aether";
        }

        public string GetPluginWebsite() {
            return "";
        }

        public string GetPluginDescription() {

            return string.Format(@" <p>If you liked my plugins, feel free to show your support.</p>
        <form action=""https://www.paypal.com/cgi-bin/webscr"" method=""post"" target=""_blank"">
        <input type=""hidden"" name=""cmd"" value=""_s-xclick"">
        <input type=""hidden"" name=""encrypted"" value=""-----BEGIN PKCS7-----MIIHVwYJKoZIhvcNAQcEoIIHSDCCB0QCAQExggEwMIIBLAIBADCBlDCBjjELMAkGA1UEBhMCVVMxCzAJBgNVBAgTAkNBMRYwFAYDVQQHEw1Nb3VudGFpbiBWaWV3MRQwEgYDVQQKEwtQYXlQYWwgSW5jLjETMBEGA1UECxQKbGl2ZV9jZXJ0czERMA8GA1UEAxQIbGl2ZV9hcGkxHDAaBgkqhkiG9w0BCQEWDXJlQHBheXBhbC5jb20CAQAwDQYJKoZIhvcNAQEBBQAEgYBCWqqEncB+6EHGzyh0x8D9DcRg1p6zeEkbeogNIexTlNmjBGVhexdpwyMnDrmUkqijrioSzM2wl7NvAz11ImzfbrwAi2ZrQ5aJkX5QTCAFUPiEK/XRlfW4oT1nNDRAnI0sODoPlPd+QQRM5pujKL9bhNU7qfrndut9CeclFqjdUTELMAkGBSsOAwIaBQAwgdQGCSqGSIb3DQEHATAUBggqhkiG9w0DBwQIoI84HLtPZo+AgbAGYg6kJH8xY2pP4ulkJly/5ry0AxQXGHmXYE04d1U9QFbaPQELtUdcPbVoQIFIRmwOSAbmWRJj341uvO1vrCtw9nBu58MZsCQZc7MOdHzbnhAKwBpu6OO9EmoAeqtyNAkCn6MaTmTahnQr4IyDfde10juR2oMkvNOkKpQhppf4pUUPhoQWK807MUwVCPY7S2qpDzWE5pSzxSeBzo23GvNZ7t3kqlNkWeorcohF9Af49KCCA4cwggODMIIC7KADAgECAgEAMA0GCSqGSIb3DQEBBQUAMIGOMQswCQYDVQQGEwJVUzELMAkGA1UECBMCQ0ExFjAUBgNVBAcTDU1vdW50YWluIFZpZXcxFDASBgNVBAoTC1BheVBhbCBJbmMuMRMwEQYDVQQLFApsaXZlX2NlcnRzMREwDwYDVQQDFAhsaXZlX2FwaTEcMBoGCSqGSIb3DQEJARYNcmVAcGF5cGFsLmNvbTAeFw0wNDAyMTMxMDEzMTVaFw0zNTAyMTMxMDEzMTVaMIGOMQswCQYDVQQGEwJVUzELMAkGA1UECBMCQ0ExFjAUBgNVBAcTDU1vdW50YWluIFZpZXcxFDASBgNVBAoTC1BheVBhbCBJbmMuMRMwEQYDVQQLFApsaXZlX2NlcnRzMREwDwYDVQQDFAhsaXZlX2FwaTEcMBoGCSqGSIb3DQEJARYNcmVAcGF5cGFsLmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAwUdO3fxEzEtcnI7ZKZL412XvZPugoni7i7D7prCe0AtaHTc97CYgm7NsAtJyxNLixmhLV8pyIEaiHXWAh8fPKW+R017+EmXrr9EaquPmsVvTywAAE1PMNOKqo2kl4Gxiz9zZqIajOm1fZGWcGS0f5JQ2kBqNbvbg2/Za+GJ/qwUCAwEAAaOB7jCB6zAdBgNVHQ4EFgQUlp98u8ZvF71ZP1LXChvsENZklGswgbsGA1UdIwSBszCBsIAUlp98u8ZvF71ZP1LXChvsENZklGuhgZSkgZEwgY4xCzAJBgNVBAYTAlVTMQswCQYDVQQIEwJDQTEWMBQGA1UEBxMNTW91bnRhaW4gVmlldzEUMBIGA1UEChMLUGF5UGFsIEluYy4xEzARBgNVBAsUCmxpdmVfY2VydHMxETAPBgNVBAMUCGxpdmVfYXBpMRwwGgYJKoZIhvcNAQkBFg1yZUBwYXlwYWwuY29tggEAMAwGA1UdEwQFMAMBAf8wDQYJKoZIhvcNAQEFBQADgYEAgV86VpqAWuXvX6Oro4qJ1tYVIT5DgWpE692Ag422H7yRIr/9j/iKG4Thia/Oflx4TdL+IFJBAyPK9v6zZNZtBgPBynXb048hsP16l2vi0k5Q2JKiPDsEfBhGI+HnxLXEaUWAcVfCsQFvd2A1sxRr67ip5y2wwBelUecP3AjJ+YcxggGaMIIBlgIBATCBlDCBjjELMAkGA1UEBhMCVVMxCzAJBgNVBAgTAkNBMRYwFAYDVQQHEw1Nb3VudGFpbiBWaWV3MRQwEgYDVQQKEwtQYXlQYWwgSW5jLjETMBEGA1UECxQKbGl2ZV9jZXJ0czERMA8GA1UEAxQIbGl2ZV9hcGkxHDAaBgkqhkiG9w0BCQEWDXJlQHBheXBhbC5jb20CAQAwCQYFKw4DAhoFAKBdMBgGCSqGSIb3DQEJAzELBgkqhkiG9w0BBwEwHAYJKoZIhvcNAQkFMQ8XDTExMTIyNTE0MTc1MlowIwYJKoZIhvcNAQkEMRYEFPYq3I9oeOtCkfJ6cpfgEYdcNdB5MA0GCSqGSIb3DQEBAQUABIGADdm0yVC3p09J1/HS7prdqq6V3xltM1kVPp0wqqLtwTyLeHID6EfdOu8ElCHf0mmNcISVHcP95Nms8TmTx2dZDcw6e2ZWR+KZFf6nHra/u99y9RYlm3Pmp4AcI0eb/mg0vkKwKSZ5+t+FKt9/bendKVujArAxSCNf2vm706/hvf0=-----END PKCS7-----"">
        <input type=""image"" src=""https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif"" border=""0"" name=""submit"" alt=""PayPal - The safer, easier way to pay online!"">
        <img alt="""" border=""0"" src=""https://www.paypalobjects.com/en_US/i/scr/pixel.gif"" width=""1"" height=""1"">
        </form>

        <h2>Description</h2>
        <p>xEquilibrium is an balancing plugin with an extremely advanced balancing algorithm but boasts a simple and effective user interface.<br>

        <h2>In-game Commands</h2><br>

        <blockquote> 
        <h4>/#</h4>While a vote is in progress, votes for the option represented by the #.
        </blockquote>

        <h2>Settings</h2><br>
        
        <br><h3>1. Join Balancer</h3>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Join Balancer</h4></font>
        <b><i>Enabled</i></b> | <i>Disabled</i><br>
        Determines the which team joining players should join based on their stats and forces them to that team.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Always Join Friends?</h4></font>
        <i>Yes</i> | <b><i>No</i></b> <br>
        Joining players will always join a friends squad, if there is room in the squad and it is not locked. Otherwise, at least the player will be placed on the same team, if there is room.
        </blockquote>
        
        <br><h3>2. Live Balancer</h3>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Live Balancer</h4></font> 
        <b><i>Enabled</i></b> | <i>Disabled</i><br>
        Detects if there is a player count or dexterity stack, then compiles a list of players that could be moved to correct the stack. If the stack is minor, then it will wait until a player on the list dies and then move them.
        If it is a major stack, then the best candidate will be forced to the other team.
        </blockquote>      

        <font color=""#FD002B""><!--RED-->
        <blockquote> 
        <h4>Keep Squads Together?</h4></font>
        <i>Yes</i> | <b><i>No</i></b> <br>
        If 'Yes', then when a balance is needed, players in squads will not be selected.
        </blockquote>

        <font color=""#FD002B""><!--RED-->
        <blockquote> 
        <h4>Keep Friends Together?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes', then when a balance is needed, players with friends on their team will not be selected.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Aggressiveness</h4></font>
        <i>Passive</i> | <b><i>Moderate</i></b> | <i>Aggressive</i> | <i>Brutal</i> | <i>Custom</i><br>
        Presets determining how aggressive the live balancer will behave in order to balance achieve even count and dexterity teams. The values that are changed by this setting can be found in the 'Expert Settings' area.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Team Changes</h4></font>
        <i>Allowed</i> | <b><i>Restricted</i></b> | <i>Disallowed</i><br>
        Determines whether players are allowed to change teams on their own accord. If 'Restricted' is set, then they can only switch teams if it would help balance the server.
        </blockquote>
        
        <br><h3>3. Round Balancer</h3>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Round Balancer</h4></font>
        <b><i>Enabled</i></b> | <i>Disabled</i> <br>
        Sorts the players into groups of friends/clans/squads and evenly assigned them to each team and into squads. Assigns the player with the highest dexterity as squad leader.
        </blockquote>

        <font color=""#FD002B""><!--RED-->
        <blockquote> 
        <h4>Keep Squads Together?</h4></font>
        <i>Yes</i> | <b><i>No</i></b> <br>
        If 'Yes', then when a round balance takes place, the current squad arrangement will be kept the same. Only unsquaded players will be balanced.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Map Bias</h4></font>
        <b><i>Enabled</i></b> | <i>Disabled</i><br>
        On maps which are unbalanced by design, the team which is more difficult to play as will be given more dexterity.
        </blockquote>
        <br>
        <h3>5. Friends</h3>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Player Squading Priority</h4></font>
        <b><i>Fewest Friends</i></b> | <i>Most Friends</i> <br>
        When xEquilibrium is forming squads from large clusters of friends, if 'Fewest Friends' is selected it will ensure the player has at least one friends in their squad. 
        If 'Most Friends' is selected the players with the most friend connections will be squaded together. Players who are friends with only one player may not end up in a squad with them.
        </blockquote>
        
        <br><h3>6. Whitelist</h3>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Whitelist</h4></font>
        <i>Enabled</i> | <b><i>Disabled</i></b> <br>
        Protects VIP players from being switched team when a balance is needed.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Include 'Reserved Slot' Members?</h4></font>
        <i>Yes</i> | <b><i>No</i></b> <br>
        If 'Yes', it will import the 'Reserved Slot' list from procon and include them in the whitelist.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Include 'Account Holder' Members?</h4></font>
        <i>Yes</i> | <b><i>No</i></b> <br>
        If 'Yes', it will import the 'Account Holder' list from procon and include them in the whitelist.
        </blockquote>

        <font color=""#FF8821""><!--ORANGE-->
        <blockquote> 
        <h4>Others List</h4></font>
        <i>String</i> <br>
        A list of case-sensitive soldiernames to also include in the whitelist.
        </blockquote>
       
        <br><h3>7. Console Output</h3>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Output Level</h4></font>
        <i>integer 0...<b>3</b>...5</i> <br>
        Determines how much information is outputed to the plugin console window. '0': no messages; '1': only shows error messages; '2': warnings and above; '3': important information and above; '4': detailed information and above; '5': all messages.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show Player Joins/Leaves?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes' player joins and leaves are shown if the 'Output Level' is at least '3'.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show Team Changes?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes' player joins and leaves are shown if the 'Output Level' is at least '3'.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show Squad Changes?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes' player joins and leaves are shown if the 'Output Level' is at least '3'.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show Team Balances?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes' player joins and leaves are shown if the 'Output Level' is at least '3'.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show All Failed Commands?</h4></font>
        <b><i>Yes</i></b> | <i>No</i> <br>
        If 'Yes' player joins and leaves are shown if the 'Output Level' is at least '3'.
        </blockquote>

        <br><h3>9. Expert Settings</h3>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Show Expert Settings?</h4></font>
        <i>Yes</i> | <b><i>No</i></b>  <br>
        Determines whether detailed expert settings are visible or not.
        </blockquote>

        <font color=""#FD002B""><!--RED-->
        <blockquote> 
        <h4>xEquilibrium | Disable xEquilibrium When Server is Unranked?</h4></font>
        <i>Yes</i> | <b><i>No</i></b>  <br>
        When the server is set to Unranked mode (usually for scrims ect.), disable the balancing functions.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Live Balancer | Player Count Difference Threshold (Wait for Death)</h4></font>
        <i>integer 1...<b>2</b>...</i> <br>
        The difference in 
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Live Balancer | Winning Team Dexterity Compensation</h4></font>
        <i>integer 1...<b>10</b>...100</i> <br>
        Estimates the winning team's dexterity higher than it actually is, in order to give the losing team more skilled players. The bias is proportional to score difference and server player count.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Live Balancer | Ticket Count Disable Threshold</h4></font>
        <i>integer 1...<b>15</b>...100</i> <br>
        Percentage from the end of the game when livebalance is disabled. This is to prevent players from getting swapped to the losing team very late in the game.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Live Balancer | Player Move Limit</h4></font>
        <i>integer 0...<b>1</b>...</i> <br>
        The maximum number of times a player can be balanced to the other team midgame, in one session on the server.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Round Balancer | Delay (s)</h4></font>
        <i>integer 0...<b>40</b>...90</i> <br>
        Time delay between the end of round and RoundBalancer commencing.
        </blockquote>

        <font color=""#7CAF4A""><!--GREEN-->
        <blockquote> 
        <h4>Round Balancer | Split Friend Clusters Over</h4></font>
        <i>integer 2...<b>8</b>...</i> <br>
        Largest allowed friend cluster to be kept intact.
        </blockquote>
                              
        <h2>Development</h2><br>
        
        <h3>Known issues</h3><br>
        <ul>
        <li>Too many to list...</li>
        </ul>

        <h3>Future Work</h3><br>
        <ul>
        <li>Flesh out code</li>
        <li>Epic testing</li>
        </ul>

        <h3>Change Log</h3><br>
        <h4>0.0.9</h4><br>
        <ul>
        <li>First Open-Beta Release</li>
        </ul>


        <p>
        <img src=""{0}"">
        </p>

        <p>
        <img src=""http://chart.apis.google.com/chart?chxl=0:|-50%25|0%25|%2B50%25&chxr=0,-5,100&chxt=r&chs=780x300&cht=lc&chco=DA3B15,F7A10A,4582E7&chd=s:_abdegedbageba,Apisomedabelprmnr,5_PRTSPNQYWROQTXYRQ&chdl=Score|Dexterity|Player+Count&chls=1|1|1&chtt=Caspian_Border+%5BCQ%5D""><br>
        </p>

        <p>
        <img src=""http://chart.apis.google.com/chart?chxl=0:|-50%25|0%25|%2B50%25&chxr=0,-5,100&chxt=r&chs=780x300&cht=lc&chco=DA3B15,F7A10A,4582E7&chd=s:_abdegedbageba,Apisomedabelprmnr,5_PRTSPNQYWROQTXYRQ&chdl=Score|Dexterity|Player+Count&chls=1|1|1&chtt=Wake+Island+%5BCQ%5D""><br>
        </p>       


        ", GenerateGraphURL(0));
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion) 
        {
            this.m_strHostName = strHostName;
            this.m_strPort = strPort;
            this.m_strPRoConVersion = strPRoConVersion;
            this.RegisterEvents(this.GetType().Name, "OnServerInfo", "OnReservedSlotsList", "OnMaplistList", "OnMaplistGetMapIndices", "OnServerName", "OnMaxPlayers", "OnGameModeCounter", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerTeamChange", "OnPlayerSquadChange", "OnPlayerMovedByAdmin", "OnGlobalChat", 
                "OnPlayerSpawned", "OnPlayerKilled", "OnRoundOver", "OnLevelLoaded", "OnRestartLevel", "OnEndRound", "OnRoundOverTeamScores", "OnRoundOverPlayers", "OnRunNextLevel", "OnResponseError");       
        }

        public void OnPluginEnable() 
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^b[" + GetPluginName() + "] ^2Enabled!");

            this.server = null;

            this.ExecuteCommand("procon.protected.tasks.add", "taskLiveBalance", "120", updateInterval.ToString(), "-1", "procon.protected.plugins.call", GetPluginName(), "LiveBalance", "true", "false");
          //  this.ExecuteCommand("procon.protected.tasks.add", "taskSkillMonitor", "120", "15", "-1", "procon.protected.plugins.call", GetPluginName(), "SkillMonitor");
            this.ExecuteCommand("procon.protected.tasks.add", "taskDumpData", "120", "120", "-1", "procon.protected.plugins.call", GetPluginName(), "ShowInformation");
            this.ExecuteCommand("procon.protected.tasks.add", "taskListPlayers", "10", "10", "-1", "procon.protected.send", "admin.listPlayers", "all");
         //   this.ExecuteCommand("procon.protected.tasks.add", "taskxEquilibriumSpam", "1800", "1800", "-1", "procon.protected.send", "admin.say", "Testing beta skill-balancer, development in progress.", "all");

            this.ExecuteCommand("procon.protected.send", "vars.maxPlayers");
            this.ExecuteCommand("procon.protected.send", "vars.serverName");
        }

        public void OnPluginDisable() 
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^b[" + GetPluginName() + "] ^2Disabled!");

            this.ExecuteCommand("procon.protected.tasks.remove", "taskLiveBalance");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskSkillMonitor");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskAntistackSpam");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskListPlayers");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskxEquilibriumSpam");

        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            lstReturn.Add(new CPluginVariable("1. Join Balancer|Join Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableJoinBalancer.ToString()));
            if (this.enableJoinBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("1. Join Balancer|Always Join Friends?", this.alwaysJoinFriends.GetType(), this.alwaysJoinFriends));
            }

            lstReturn.Add(new CPluginVariable("2. Live Balancer|Live Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableLiveBalancer.ToString()));
            if (this.enableLiveBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("2. Live Balancer|Keep Squads Together?", this.liveKeepSquadsTogether.GetType(), this.liveKeepSquadsTogether));
                lstReturn.Add(new CPluginVariable("2. Live Balancer|Show Announcement?", this.showPlayerJoins.GetType(), this.showPlayerJoins));
                lstReturn.Add(new CPluginVariable("2. Live Balancer|Aggressiveness", "enum.Aggressiveness(Passive|Moderate|Aggressive|Brutal|Custom)", this.liveAggressiveness.ToString()));             
            }
            lstReturn.Add(new CPluginVariable("2. Live Balancer|Team Changes", "enum.TeamChanges(Allowed|Restricted|Disallowed)", this.allowTeamChanges.ToString()));

            lstReturn.Add(new CPluginVariable("3. Round Balancer|Round Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableRoundBalancer.ToString()));
            if (this.enableRoundBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("3. Round Balancer|Keep Squads Together? ", this.roundKeepSquadsTogether.GetType(), this.roundKeepSquadsTogether));
                lstReturn.Add(new CPluginVariable("3. Round Balancer|Map Bias", "enum.EnabledDisabled(Enabled|Disabled)", this.enableMapBias.ToString()));
                lstReturn.Add(new CPluginVariable("3. Round Balancer|Balancing Round (fix me)", "enum.BalancingRound(Every Round|Last Round)", this.allowTeamChanges.ToString()));
            }

            lstReturn.Add(new CPluginVariable("5. Friends|Squad Player Priority", "enum.FriendsGrouping(Fewest Friends|Most Friends)", this.friendsGrouping));

            if (this.enableLiveBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("6. Whitelist|Whitelist", "enum.EnabledDisabled(Enabled|Disabled)", this.enableWhitelist.ToString()));
                if (this.enableWhitelist == State.Enabled)
                {
                    lstReturn.Add(new CPluginVariable("6. Whitelist|Include 'Reserved Slot' Members?", this.includeReserved.GetType(), this.includeReserved));
                    lstReturn.Add(new CPluginVariable("6. Whitelist|Include 'Account Holder' Members?", this.includeAccounts.GetType(), this.includeAccounts));
                    lstReturn.Add(new CPluginVariable("6. Whitelist|Others List", typeof(string[]), this.liveWhitelist.ToArray()));
                    lstReturn.Add(new CPluginVariable("6. Whitelist|Protect:", "enum.Whitelist(Player Only|Entire Squad)", this.allowTeamChanges.ToString()));
                }
            }

            lstReturn.Add(new CPluginVariable("7. Console Output|Output Level", this.globalDebugLevel.GetType(), this.globalDebugLevel));
            if (this.isExpert == enumBoolYesNo.Yes && this.globalDebugLevel >= 2)
            {
                lstReturn.Add(new CPluginVariable("7. Console Output|Show Player Joins/Leaves?", this.showPlayerJoins.GetType(), this.showPlayerJoins));
                lstReturn.Add(new CPluginVariable("7. Console Output|Show Team Changes?", this.showTeamChanges.GetType(), this.showTeamChanges));
                lstReturn.Add(new CPluginVariable("7. Console Output|Show Squad Changes?", this.showSquadChanges.GetType(), this.showSquadChanges));
                if (this.enableLiveBalancer == State.Enabled)
                {
                    lstReturn.Add(new CPluginVariable("7. Console Output|Show LiveBalance Monitor?", this.showLiveMonitor.GetType(), this.showLiveMonitor));
                    lstReturn.Add(new CPluginVariable("7. Console Output|Show Team Balances?", this.showTeamBalances.GetType(), this.showTeamBalances));
                }
                lstReturn.Add(new CPluginVariable("7. Console Output|Show All Failed Commands?", this.showFailedCommands.GetType(), this.showFailedCommands));
            }

            string otherServersEnum = GetOtherServersEnum();
            lstReturn.Add(new CPluginVariable("8. Import Settings|Import Settings", otherServersEnum, "Select Server..."));
            lstReturn.Add(new CPluginVariable("8. xEquilibrium Console|Console", typeof(string), ""));

            if (this.enableLiveBalancer == State.Enabled || this.enableRoundBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Show Expert Settings?", this.isExpert.GetType(), this.isExpert));
            }

            if (this.isExpert == enumBoolYesNo.Yes)
            {
                lstReturn.Add(new CPluginVariable("9. Expert Settings|xEquilibrium | Disable xEquilibrium When Server is Unranked?", this.disableInUnranked.GetType(), this.disableInUnranked));
            }

            if (this.isExpert == enumBoolYesNo.Yes && this.enableLiveBalancer == State.Enabled)
            {               
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Future Player Count Difference Threshold (Wait for Death)", this.futurePlayerCountDiffWaitThreshold.GetType(), this.futurePlayerCountDiffWaitThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Current Player Count Difference Threshold (Wait for Death)", this.currentPlayerCountDiffWaitThreshold.GetType(), this.currentPlayerCountDiffWaitThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Future Player Count Difference Threshold (Force Kill)", this.futurePlayerCountDiffKillThreshold.GetType(), this.futurePlayerCountDiffKillThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Current Player Count Difference Threshold (Force Kill)", this.currentPlayerCountDiffKillThreshold.GetType(), this.currentPlayerCountDiffKillThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Future Team Dexterity Difference Threshold (Wait for Death)", this.futureDexDiffWaitThreshold.GetType(), this.futureDexDiffWaitThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Current Team Dexterity Difference Threshold (Wait for Death)", this.currentDexDiffWaitThreshold.GetType(), this.currentDexDiffWaitThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Future Team Dexterity Difference Threshold (Force Kill)", this.futureDexDiffKillThreshold.GetType(), this.futureDexDiffKillThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Current Team Dexterity Difference Threshold (Force Kill)", this.currentDexDiffKillThreshold.GetType(), this.currentDexDiffKillThreshold));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Winning Team Dexterity Compensation", this.winningSkillComp.GetType(), this.winningSkillComp));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Ticket Count Disable Threshold", this.disableTicketThres.GetType(), this.disableTicketThres));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Player Move Limit", this.playerMoveLimit.GetType(), this.playerMoveLimit));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Live Balancer | Update Interval", this.updateInterval.GetType(), this.updateInterval));
            }
            if (this.isExpert == enumBoolYesNo.Yes && this.enableRoundBalancer == State.Enabled)
            {
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Round Balancer | Delay (s)", this.roundTimeDelay.GetType(), this.roundTimeDelay));
                lstReturn.Add(new CPluginVariable("9. Expert Settings|Round Balancer | Split Friend Clusters Over", this.splitFriendsThreshold.GetType(), this.splitFriendsThreshold));
            }

            

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables() {

            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            // 1. xEquilibrium
            lstReturn.Add(new CPluginVariable("Join Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableJoinBalancer.ToString()));
            lstReturn.Add(new CPluginVariable("Always Join Friends?", this.alwaysJoinFriends.GetType(), this.alwaysJoinFriends));
            lstReturn.Add(new CPluginVariable("Live Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableLiveBalancer.ToString()));
            lstReturn.Add(new CPluginVariable("Keep Squads Together?", this.liveKeepSquadsTogether.GetType(), this.liveKeepSquadsTogether));
            lstReturn.Add(new CPluginVariable("Aggressiveness", "enum.Aggressiveness(Passive|Moderate|Aggressive|Brutal|Custom)", this.liveAggressiveness));
            lstReturn.Add(new CPluginVariable("Team Changes", "enum.TeamChanges(Allowed|Restricted|Disallowed)", this.allowTeamChanges.ToString()));
            lstReturn.Add(new CPluginVariable("Round Balancer", "enum.EnabledDisabled(Enabled|Disabled)", this.enableRoundBalancer.ToString()));
            lstReturn.Add(new CPluginVariable("Map Bias", "enum.EnabledDisabled(Enabled|Disabled)", this.enableMapBias.ToString()));

            // 5. Friends
            lstReturn.Add(new CPluginVariable("Squad Player Priority", "enum.FriendsGrouping(Fewest Friends|Most Friends)", this.friendsGrouping));

            // 6. Whitelist
            lstReturn.Add(new CPluginVariable("Whitelist", "enum.EnabledDisabled(Enabled|Disabled)", this.enableWhitelist.ToString()));
            lstReturn.Add(new CPluginVariable("Include 'Reserved Slot' Members?", this.includeReserved.GetType(), this.includeReserved));
            lstReturn.Add(new CPluginVariable("Include 'Account Holder' Members?", this.includeAccounts.GetType(), this.includeAccounts));
            lstReturn.Add(new CPluginVariable("Others List", typeof(string[]), this.liveWhitelist.ToArray()));

            // 7. Console Output
            lstReturn.Add(new CPluginVariable("Output Level", this.globalDebugLevel.GetType(), this.globalDebugLevel));
            lstReturn.Add(new CPluginVariable("Show Player Joins/Leaves?", this.showPlayerJoins.GetType(), this.showPlayerJoins));
            lstReturn.Add(new CPluginVariable("Show Team Changes?", this.showTeamChanges.GetType(), this.showTeamChanges));
            lstReturn.Add(new CPluginVariable("Show Squad Changes?", this.showSquadChanges.GetType(), this.showSquadChanges));
            lstReturn.Add(new CPluginVariable("Show LiveBalance Monitor?", this.showLiveMonitor.GetType(), this.showLiveMonitor));
            lstReturn.Add(new CPluginVariable("Show Team Balances?", this.showTeamBalances.GetType(), this.showTeamBalances));
            lstReturn.Add(new CPluginVariable("Show All Failed Commands?", this.showFailedCommands.GetType(), this.showFailedCommands));

            // 9. Expert Settings
            lstReturn.Add(new CPluginVariable("Show Expert Settings?", this.isExpert.GetType(), this.isExpert));

            lstReturn.Add(new CPluginVariable("xEquilibrium | Disable xEquilibrium When Server is Unranked?", this.disableInUnranked.GetType(), this.disableInUnranked));

            lstReturn.Add(new CPluginVariable("Live Balancer | Future Player Count Difference Threshold (Wait for Death)", this.futurePlayerCountDiffWaitThreshold.GetType(), this.futurePlayerCountDiffWaitThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Current Player Count Difference Threshold (Wait for Death)", this.currentPlayerCountDiffWaitThreshold.GetType(), this.currentPlayerCountDiffWaitThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Future Player Count Difference Threshold (Force Kill)", this.futurePlayerCountDiffKillThreshold.GetType(), this.futurePlayerCountDiffKillThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Current Player Count Difference Threshold (Force Kill)", this.currentPlayerCountDiffKillThreshold.GetType(), this.currentPlayerCountDiffKillThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Future Team Dexterity Difference Threshold (Wait for Death)", this.futureDexDiffWaitThreshold.GetType(), this.futureDexDiffWaitThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Current Team Dexterity Difference Threshold (Wait for Death)", this.currentDexDiffWaitThreshold.GetType(), this.currentDexDiffWaitThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Future Team Dexterity Difference Threshold (Force Kill)", this.futureDexDiffKillThreshold.GetType(), this.futureDexDiffKillThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Current Team Dexterity Difference Threshold (Force Kill)", this.currentDexDiffKillThreshold.GetType(), this.currentDexDiffKillThreshold));
            lstReturn.Add(new CPluginVariable("Live Balancer | Winning Team Dexterity Compensation", this.winningSkillComp.GetType(), this.winningSkillComp));
            lstReturn.Add(new CPluginVariable("Live Balancer | Ticket Count Disable Threshold", this.disableTicketThres.GetType(), this.disableTicketThres));
            lstReturn.Add(new CPluginVariable("Live Balancer | Player Move Limit", this.playerMoveLimit.GetType(), this.playerMoveLimit));
            lstReturn.Add(new CPluginVariable("Live Balancer | Update Interval", this.updateInterval.GetType(), this.updateInterval));

            lstReturn.Add(new CPluginVariable("Round Balancer | Delay (s)", this.roundTimeDelay.GetType(), this.roundTimeDelay));
            lstReturn.Add(new CPluginVariable("Round Balancer | Split Friend Clusters Over", this.splitFriendsThreshold.GetType(), this.splitFriendsThreshold));

            // Round Statistics
            //lstReturn.Add(new CPluginVariable("Round0: Map", typeof(string), "Operation Metro"));
            //lstReturn.Add(new CPluginVariable("Round0: Score", typeof(string[]), this.liveWhitelist.ToArray()));
            //lstReturn.Add(new CPluginVariable("Round0: PlayerCount", typeof(string[]), this.liveWhitelist.ToArray()));
            //lstReturn.Add(new CPluginVariable("Round0: Dexterity", typeof(string[]), this.liveWhitelist.ToArray()));

            // others
            lstReturn.Add(new CPluginVariable("shownSquadTogetherWarning", this.shownSquadTogetherWarning.GetType(), this.shownSquadTogetherWarning));
            lstReturn.Add(new CPluginVariable("serverName", this.serverName.GetType(), this.serverName));

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue) {

            int value = 0;
            double dvalue = 0;
            bool bValue = false;

            // 1. xEquilibrium
            if (strVariable.CompareTo("Join Balancer") == 0 && Enum.IsDefined(typeof(State), strValue) == true)
            {
                this.enableJoinBalancer = (State)Enum.Parse(typeof(State), strValue);
            }
            else if (strVariable.CompareTo("Always Join Friends?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.alwaysJoinFriends = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Live Balancer") == 0 && Enum.IsDefined(typeof(State), strValue) == true)
            {
                this.enableLiveBalancer = (State)Enum.Parse(typeof(State), strValue);
            }
            else if (strVariable.CompareTo("Keep Squads Together?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                enumBoolYesNo ynValue = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
                if (ynValue == enumBoolYesNo.Yes && liveKeepSquadsTogether == enumBoolYesNo.No && !this.shownSquadTogetherWarning)
                {
                    DialogResult dr = MessageBox.Show("Are you sure?\n\nThis will limit xEquilibium's ability to balance the teams.\nYou will still be kept together with your friends with 'Keep Squads Together?' set to 'No'.", "Confirm Change", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dr == DialogResult.Yes)
                    {
                        liveKeepSquadsTogether = enumBoolYesNo.Yes;
                    }
                    shownSquadTogetherWarning = true;
                }
                else
                {
                    liveKeepSquadsTogether = ynValue;
                }
            }
            else if (strVariable.CompareTo("Aggressiveness") == 0)
            {
                this.liveAggressiveness = strValue;
                switch (strValue)
                {
                    case "Passive":
                        this.futurePlayerCountDiffWaitThreshold = 4;
                        this.futureDexDiffWaitThreshold = 3000;
                        this.winningSkillComp = 1;
                        this.disableTicketThres = 25;
                        this.playerMoveLimit = 1;
                        this.splitFriendsThreshold = 32;

                        break;
                    case "Moderate":
                        this.futurePlayerCountDiffWaitThreshold = 2;
                        this.futureDexDiffWaitThreshold = 1000;
                        this.winningSkillComp = 10;
                        this.disableTicketThres = 15;
                        this.playerMoveLimit = 1;
                        this.splitFriendsThreshold = 8;

                        break;
                    case "Aggressive":
                        this.futurePlayerCountDiffWaitThreshold = 2;
                        this.futureDexDiffWaitThreshold = 750;
                        this.winningSkillComp = 15;
                        this.disableTicketThres = 15;
                        this.playerMoveLimit = 1;
                        this.splitFriendsThreshold = 4;

                        break;
                    case "Brutal":
                        this.futurePlayerCountDiffWaitThreshold = 1;
                        this.futureDexDiffWaitThreshold = 500;
                        this.winningSkillComp = 20;
                        this.disableTicketThres = 10;
                        this.playerMoveLimit = 2;
                        this.splitFriendsThreshold = 4;

                        break;
                    case "Custom":
                    default:
                        break;
                }
            }
            else if (strVariable.CompareTo("Team Changes") == 0 && Enum.IsDefined(typeof(RState), strValue) == true)
            {
                this.allowTeamChanges = (RState)Enum.Parse(typeof(RState), strValue);
            }
            else if (strVariable.CompareTo("Round Balancer") == 0 && Enum.IsDefined(typeof(State), strValue) == true)
            {
                this.enableRoundBalancer = (State)Enum.Parse(typeof(State), strValue);
            }
            else if (strVariable.CompareTo("Map Bias") == 0 && Enum.IsDefined(typeof(State), strValue) == true)
            {
                this.enableMapBias = (State)Enum.Parse(typeof(State), strValue);
            }

            // 5. Friends
            else if (strVariable.CompareTo("Squad Player Priority") == 0)
            {
                this.friendsGrouping = strValue;
            }

            // 6. Whitelist
            else if (strVariable.CompareTo("Whitelist") == 0 && Enum.IsDefined(typeof(State), strValue) == true)
            {
                this.enableWhitelist = (State)Enum.Parse(typeof(State), strValue);
            }
            else if (strVariable.CompareTo("Include 'Reserved Slot' Members?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.includeReserved = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Include 'Account Holder' Members?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.includeAccounts = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Others List") == 0)
            {
                this.liveWhitelist = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }

            // 7. Console Output
            else if (strVariable.CompareTo("Output Level") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.globalDebugLevel = value;
            }
            else if (strVariable.CompareTo("Show Player Joins/Leaves?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showPlayerJoins = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Show Team Changes?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showTeamChanges = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Show Squad Changes?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showSquadChanges = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Show LiveBalance Monitor?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showLiveMonitor = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Show Team Balances?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showTeamBalances = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Show All Failed Commands?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showFailedCommands = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }

            // 8.
            else if (strVariable.CompareTo("Import Settings") == 0 && strValue.CompareTo("Select Server...") != 0)
            {
                SetOtherServerSettings(strValue);
            }
            else if (strVariable.CompareTo("Console") == 0)
            {
                ProcessConsoleCommand(strValue);
            }

            // 9. Expert Settings
            else if (strVariable.CompareTo("Show Expert Settings?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.isExpert = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("xEquilibrium | Disable xEquilibrium When Server is Unranked?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.disableInUnranked = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Live Balancer | Future Player Count Difference Threshold (Wait for Death)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.futurePlayerCountDiffWaitThreshold = value;
                if (this.futurePlayerCountDiffWaitThreshold < 1)
                {
                    this.futurePlayerCountDiffWaitThreshold = 1;
                }
                if (this.futurePlayerCountDiffWaitThreshold > this.futurePlayerCountDiffKillThreshold)
                {
                    this.futurePlayerCountDiffKillThreshold = this.futurePlayerCountDiffWaitThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Current Player Count Difference Threshold (Wait for Death)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.currentPlayerCountDiffWaitThreshold = value;
                if (this.currentPlayerCountDiffWaitThreshold < 1)
                {
                    this.currentPlayerCountDiffWaitThreshold = 1;
                }
                if (this.currentPlayerCountDiffWaitThreshold > this.currentPlayerCountDiffKillThreshold)
                {
                    this.currentPlayerCountDiffKillThreshold = this.currentPlayerCountDiffWaitThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Future Player Count Difference Threshold (Force Kill)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.futurePlayerCountDiffKillThreshold = value;
                if (this.futurePlayerCountDiffKillThreshold < 1)
                {
                    this.futurePlayerCountDiffKillThreshold = 1;
                }
                if (this.futurePlayerCountDiffWaitThreshold > this.futurePlayerCountDiffKillThreshold)
                {
                    this.futurePlayerCountDiffWaitThreshold = this.futurePlayerCountDiffKillThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Current Player Count Difference Threshold (Force Kill)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.currentPlayerCountDiffKillThreshold = value;
                if (this.currentPlayerCountDiffKillThreshold < 1)
                {
                    this.currentPlayerCountDiffKillThreshold = 1;
                }
                if (this.currentPlayerCountDiffWaitThreshold > this.currentPlayerCountDiffKillThreshold)
                {
                    this.currentPlayerCountDiffWaitThreshold = this.currentPlayerCountDiffKillThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Future Team Dexterity Difference Threshold (Wait for Death)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.futureDexDiffWaitThreshold = value;
                if (this.futureDexDiffWaitThreshold < 10)
                {
                    this.futureDexDiffWaitThreshold = 10;
                }
                if (this.futureDexDiffWaitThreshold > this.futureDexDiffKillThreshold)
                {
                    this.futureDexDiffKillThreshold = this.futureDexDiffWaitThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Current Team Dexterity Difference Threshold (Wait for Death)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.currentDexDiffWaitThreshold = value;
                if (this.currentDexDiffWaitThreshold < 10)
                {
                    this.currentDexDiffWaitThreshold = 10;
                }
                if (this.currentDexDiffWaitThreshold > this.currentDexDiffKillThreshold)
                {
                    this.currentDexDiffKillThreshold = this.currentDexDiffWaitThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Future Team Dexterity Difference Threshold (Force Kill)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.futureDexDiffKillThreshold = value;
                if (this.futureDexDiffKillThreshold < 10)
                {
                    this.futureDexDiffKillThreshold = 10;
                }
                if (this.futureDexDiffWaitThreshold > this.futureDexDiffKillThreshold)
                {
                    this.futureDexDiffWaitThreshold = this.futureDexDiffKillThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Current Team Dexterity Difference Threshold (Force Kill)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.currentDexDiffKillThreshold = value;
                if (this.currentDexDiffKillThreshold < 10)
                {
                    this.currentDexDiffKillThreshold = 10;
                }
                if (this.currentDexDiffWaitThreshold > this.currentDexDiffKillThreshold)
                {
                    this.currentDexDiffWaitThreshold = this.currentDexDiffKillThreshold;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Winning Team Dexterity Compensation") == 0 && double.TryParse(strValue, out dvalue) == true)
            {
                this.winningSkillComp = dvalue;
                if (this.winningSkillComp < 0)
                {
                    this.winningSkillComp = 0;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Ticket Count Disable Threshold") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.disableTicketThres = value;
                if (this.disableTicketThres < 0)
                {
                    this.disableTicketThres = 0;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Player Move Limit") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.playerMoveLimit = value;
                if (this.playerMoveLimit < 0)
                {
                    this.playerMoveLimit = 0;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }
            else if (strVariable.CompareTo("Live Balancer | Update Interval") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.updateInterval = value;
                if (this.updateInterval < 1)
                {
                    this.updateInterval = 1;
                }
            }
            else if (strVariable.CompareTo("Round Balancer | Delay (s)") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.roundTimeDelay = value;
                if (this.roundTimeDelay < 0)
                {
                    this.roundTimeDelay = 0;
                }
                else if (this.roundTimeDelay > 90)
                {
                    this.roundTimeDelay = 90;
                }
            }
            else if (strVariable.CompareTo("Round Balancer | Split Friend Clusters Over") == 0 && int.TryParse(strValue, out value) == true)
            {
                this.splitFriendsThreshold = value;
                if (this.splitFriendsThreshold < 1)
                {
                    this.splitFriendsThreshold = 1;
                }
                if (this.isExpert == enumBoolYesNo.Yes)
                {
                    this.liveAggressiveness = "Custom";
                }
            }

            
            // Others
            else if (strVariable.CompareTo("shownSquadTogetherWarning") == 0 && bool.TryParse(strValue, out bValue) == true)
            {
                this.shownSquadTogetherWarning = bValue;
            }
            else if (strVariable.CompareTo("serverName") == 0 && this.serverName.CompareTo("Undefined") == 0)
            {
                this.serverName = strValue;
            }
        }

        #endregion

		#region Plugin Events
		
		#endregion		

        #region Client Events

        #endregion

        #region Game Events

        public void OnServerInfo(CServerInfo csiServerInfo)
        {
            WritePluginConsole("OnServerInfo Starting...", "Info", 5);
            try
            {
                if (this.server != null && csiServerInfo.TeamScores != null && csiServerInfo.TeamScores.Count > 0)
                {
                    if (csiServerInfo.TeamScores != null && this.server.TeamScores != null & lastDelta.AddSeconds(20) < DateTime.Now)
                    {
                        int[] ticketDelta = new int[2];
                        ticketDelta[0] = (int)((this.server.TeamScores[0].Score - csiServerInfo.TeamScores[0].Score) / (DateTime.Now - lastDelta).TotalMinutes);
                        ticketDelta[1] = (int)((this.server.TeamScores[1].Score - csiServerInfo.TeamScores[1].Score) / (DateTime.Now - lastDelta).TotalMinutes);

                        WritePluginConsole("^6Team 1^0 Ticket Delta: ^5" + ticketDelta[0] + "^0 |  ^6Team 2^0 Ticket Delta: ^5" + ticketDelta[1], "Info", 4);
                        WritePluginConsole("Player Stats Fetches Remaining: ^5" + this.server.NoStatsCount, "Info", 4);
                        lastDelta = DateTime.Now;
                    }
                    this.server.TeamScores = new List<TeamScore>(csiServerInfo.TeamScores);


                    if (!nextMapShown && (csiServerInfo.TeamScores[0].Score < 30 || csiServerInfo.TeamScores[1].Score < 30))
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", "=============================", "all");
                        this.ExecuteCommand("procon.protected.send", "admin.say", "Next Map: " + GetMapByFilename(this.server.NextMap).PublicLevelName, "all");
                        this.ExecuteCommand("procon.protected.send", "admin.say", "=============================", "all");
                        nextMapShown = true;

                        WritePluginConsole("Next Map Shown!", "Work", 3);
                    }
                    this.server.CurrentMap = csiServerInfo.Map;
                    this.server.CurrentMode = csiServerInfo.GameMode;
                    this.server.WinningTeamBiasSetting = this.winningSkillComp;
                    this.server.DisableTicketThreshold = this.disableTicketThres;


                    int remaining = 0;

                    foreach (xPlayer player in server)
                    {
                        if (player.Moves < this.playerMoveLimit)
                        {
                            remaining++;
                        }
                        if (!player.GotStats && player.StatsError != "")
                        {
                            WritePluginConsole("An error occurred while fetching ^7" + player.Name + "'s stats. " + player.StatsError, "Warning", 3);
                            player.StatsError = "";
                        }
                    }

                    WritePluginConsole("LiveBalance: " + remaining + " out of " + this.server.PlayerCount + " could be moved", "Info", 4);
                }

                this.ExecuteCommand("procon.protected.send", "mapList.list");
                this.ExecuteCommand("procon.protected.send", "mapList.getMapIndices");
                this.ExecuteCommand("procon.protected.send", "reservedSlotsList.list");
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in OnServerInfo", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
            WritePluginConsole("OnServerInfo Done!", "Info", 5);

        }

        public void OnReservedSlotsList(List<string> soldierNames)
        {
            try
            {
                if (this.server != null)
                {
                    lock (serverLock)
                    {
                        int whitelistCount = 0;
                        for (int i = 0; i < this.server.PlayerCount; i++)
                        {
                            CPrivileges cp = this.GetAccountPrivileges(this.server[i].Name);

                            if (enableWhitelist == State.Enabled && (this.includeReserved == enumBoolYesNo.Yes && (soldierNames.Contains(this.server[i].Name))) || this.liveWhitelist.Contains(this.server[i].Name) || (this.includeAccounts == enumBoolYesNo.Yes && cp != null && cp.CanLogin))
                            {
                                this.server[i].Whitelisted = true;
                                whitelistCount++;
                                WritePluginConsole("Added " + this.server[i].Name + " to the whitelist.", "Info", 5);
                            }
                            else
                            {
                                this.server[i].Whitelisted = false;
                            }
                        }
                        WritePluginConsole("There are ^5" + whitelistCount + "^0 whitelisted players currently on the server.", "Info", 5);
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Caught Exception in OnReservedSlotsList", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnServerName(string serverName)
        {
            this.serverName = serverName;
        }

        public void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            try
            {
                if (this.server != null)
                {
                    this.server.CurrentMaplist = new List<MaplistEntry>(lstMaplist);
                    WritePluginConsole("Maplist updated. There are " + lstMaplist.Count + " maps currently in the maplist", "Info", 5);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Caught Exception in OnMaplistList", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

        }

        public void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            if (this.server != null)
            {
                this.server.NextMap = this.server.CurrentMaplist[nextIndex].MapFileName;
                this.server.NextMode = this.server.CurrentMaplist[nextIndex].Gamemode;
            }
        }

        public void OnListPlayers(List<CPlayerInfo> lstPlayers, CPlayerSubset cpsSubset)
        {
            try
            {
                if (this.server == null)
                {
                    this.server = new xServer(lstPlayers, this);



                    //server.AddPlayer("xSnakeDaddyx");
                    //server.AddPlayer("Grassyhopper");
                    //server.AddPlayer("5poony");
                    //server.AddPlayer("g3noc1de");
                    //server.AddPlayer("thor2222");
                    //server.AddPlayer("Thunderofwar");
                    //server.AddPlayer("aether_nz");
                    //server.AddPlayer("Buzzrker");
                    //server.AddPlayer("Deflag99");
                    //server.AddPlayer("vadimalk");
                    //server.AddPlayer("123EAM321");
                    //server.AddPlayer("FKASUMBODIEE");
                    //server.AddPlayer("Acer_Spadez");
                    //server.AddPlayer("cccubs");
                    //server.AddPlayer("Polodobo");
                    //server.AddPlayer("Team-Jericho");
                    //server.AddPlayer("Lammiwinks");
                    //server.AddPlayer("smegg_123");
                    //server.AddPlayer("ThanNZ");
                    //server.AddPlayer("Rahui_Pokeka");
                }
                else if (server.PlayerCount != lstPlayers.Count)
                {
                    // any new players?
                    foreach (CPlayerInfo cpi in lstPlayers)
                    {
                        bool found = false;
                        for (int i = 0; i < server.PlayerCount; i++)
                        {
                            if (server[i].Name == cpi.SoldierName)
                            {
                                found = true;
                                server[i].Expires = DateTime.Now.AddMinutes(5);
                                break;
                            }
                        }
                        if (!found)
                        {
                            if (showPlayerJoins == enumBoolYesNo.Yes)
                            {
                                WritePluginConsole("^7" + cpi.SoldierName + "^0 joined, finally.", "Info", 3);
                            }
                            this.server.AddPlayer(cpi.SoldierName);
                        }
                    }

                    //remove players
                    for (int i = 0; i < server.PlayerCount; i++)
                    {
                        bool found = false;
                        foreach (CPlayerInfo cpi in lstPlayers)
                        {
                            if (cpi.SoldierName == server[i].Name)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found && server[i].Expires < DateTime.Now)
                        {
                            if (showPlayerJoins == enumBoolYesNo.Yes)
                            {
                                WritePluginConsole("^7" + server[i].Name + "^0 timed out, removing...", "Info", 3);
                            }
                            server.RemovePlayer(server[i].Name);
                            i--;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in OnListPlayers", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnMaxPlayers(int limit)
        {
            if (this.server != null)
            {
                this.server.MaxPlayerCount = limit;
                WritePluginConsole("Max players: " + this.server.MaxPlayerCount, "Info", 5);
            }
        }

        public void OnGameModeCounter(int limit)
        {
            if (this.server != null)
            {
                this.server.GameModeCount = limit;
                WritePluginConsole("GameModeCounter: " + limit, "Info", 5);
            }
        }

        public void OnPlayerJoin(string soldierName)
        {
            try
            {
                if (server != null)
                {
                    lock (serverLock)
                    {
                        if (this.showPlayerJoins == enumBoolYesNo.Yes)
                        {
                            WritePluginConsole("^7" + soldierName + "^0 joining" + (server.TeamCount(0) == 0 ? "" : (" and " + server.TeamCount(0).ToString() + " other(s)")) + "...", "Info", 3);
                        }
                        server.AddPlayer(soldierName);
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in OnPlayerJoin", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnPlayerLeft(CPlayerInfo cpiPlayer)
        {
            if (this.showPlayerJoins == enumBoolYesNo.Yes)
            {
                WritePluginConsole("^7" + cpiPlayer.SoldierName + "^0 left the server.", "Info", 3);
            }

            // remove from player database
            lock (serverLock)
            {
                server.RemovePlayer(cpiPlayer.SoldierName);
            }

            // remove any pending team changes
            for (int i = 0; i < this.teamChanges.Count; i++)
            {
                if (this.teamChanges[i].Name == cpiPlayer.SoldierName)
                {
                    teamChanges.RemoveAt(i);
                    break;
                }
            }
        }

        public void OnPlayerTeamChange(string soldierName, int teamId, int squadId)
        {
            try
            {
                if (server != null)
                {
                    if (enableJoinBalancer == State.Enabled && this.server[soldierName].TeamId == 0)
                    {
                        JoinSetTargetTeam(soldierName);

                        if (server[soldierName].TargetTeam != teamId)
                        {
                            ProconMove(soldierName, server[soldierName].TargetTeam, server[soldierName].TargetSquad, true);
                        }
                    }

                    bool found = false;
                    for (int i = 0; i < this.teamChanges.Count; i++)
                    {
                        if (this.teamChanges[i].Name == soldierName)
                        {
                            found = true;
                            this.teamChanges[i].EndTeam = teamId;
                            this.teamChanges[i].EndSquad = squadId;
                        }
                    }
                    if (!found)
                    {
                        this.teamChanges.Add(new TeamChange(soldierName, this.server[soldierName].TeamId, this.server[soldierName].SquadId, teamId, squadId));
                    }

                    // // Detect end of autobalance
                    //if (DateTime.Now < autobalanceExpire && this.server[soldierName].TeamId != 0 && this.server[soldierName].TeamId != teamId)
                    //{
                    //    autobalanceExpire = DateTime.Now.AddSeconds(1);

                    //    this.ExecuteCommand("procon.protected.tasks.remove", "taskPerfectLiveBalance");
                    //    this.ExecuteCommand("procon.protected.tasks.add", "taskPerfectLiveBalance", "1", "1", "1", "procon.protected.plugins.call", GetPluginName(), "LiveBalance", "true", "true");
                    //}
                    //// anti-autobalance
                    //if (enableRoundBalancer == State.Enabled && DateTime.Now < autobalanceExpire && this.server[soldierName].TeamId != 0 && teamId != this.server[soldierName].TargetTeam)
                    //{
                    //    WritePluginConsole("I think ^7" + soldierName + "^0 was autobalanced, moving to target team", "Info", 3);
                    //    ProconMove(soldierName, server[soldierName].TargetTeam, server[soldierName].TargetSquad, true);
                    //}

                    lock (serverLock)
                    {
                        this.server[soldierName].TeamId = teamId;
                        this.server[soldierName].SquadId = squadId;
                    }

                    ProcessTeamChanges();
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in OnPlayerTeamChange", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnPlayerSquadChange(string soldierName, int teamId, int squadId)
        {
            if (server != null)
            {
                bool found = false;
                for (int i = 0; i < this.teamChanges.Count; i++)
                {

                    if (this.teamChanges[i].Name == soldierName)
                    {
                        found = true;
                        this.teamChanges[i].EndTeam = teamId;
                        this.teamChanges[i].EndSquad = squadId;
                    }
                }
                if (!found)
                {
                    this.teamChanges.Add(new TeamChange(soldierName, this.server[soldierName].TeamId, this.server[soldierName].SquadId, teamId, squadId));
                }

                lock (serverLock)
                {
                    this.server[soldierName].TeamId = teamId;
                    this.server[soldierName].SquadId = squadId;
                }

                ProcessTeamChanges();
            }
        }

        public void OnPlayerMovedByAdmin(string soldierName, int teamId, int squadId, bool forceKilled)
        {
            bool found = false;
            for (int i = 0; i < this.teamChanges.Count; i++)
            {
                if (this.teamChanges[i].Name == soldierName)
                {
                    found = true;
                    this.teamChanges[i].EndTeam = teamId;
                    this.teamChanges[i].EndSquad = squadId;
                    this.teamChanges[i].AdminMoved = true;
                }
            }
            if (!found)
            {
                TeamChange tc = new TeamChange(soldierName, this.server[soldierName].TeamId, this.server[soldierName].SquadId, teamId, squadId);
                tc.AdminMoved = true;
                this.teamChanges.Add(tc);
            }

            lock (serverLock)
            {
                this.server[soldierName].TeamId = teamId;
                this.server[soldierName].SquadId = squadId;
                this.server[soldierName].TargetTeam = teamId;
                this.server[soldierName].TargetSquad = squadId;

                if (forceKilled)
                {
                    this.server[soldierName].Alive = false;
                }
            }

            ProcessTeamChanges();
        }

        public void OnPlayerSpawned(string soldierName, Inventory spawnedInventory)
        {
            lock (serverLock)
            {
                server[soldierName].Alive = true;
            }
        }

        public void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            try
            {
                if (server != null)
                {
                    server[kKillerVictimDetails.Victim.SoldierName].Alive = false;

                    xPlayer player = PlayerFromName(this.playersToMove, kKillerVictimDetails.Victim.SoldierName);
                    if (player != null && !liveWhitelist.Contains(player.Name))
                    {
                        int squadId = server.GetFullestSquadId(player.TargetTeam);
                        ProconMove(player.Name, player.TargetTeam, squadId, false);
                        server[player.Name].Moves++;
                        if (showTeamBalances == enumBoolYesNo.Yes)
                        {
                            WritePluginConsole("^7" + player.Name + "^0 died, using to balance team " + player.TargetTeam + ".", "Info", 2);
                        }
                        this.playersToMove = new List<xPlayer>();
                        this.ExecuteCommand("procon.protected.tasks.add", "taskLiveBalance1", "1", "1", "1", "procon.protected.plugins.call", GetPluginName(), "LiveBalance", "true", "false");
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in OnPlayerKilled", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnGlobalChat(string speaker, string message)
        {
            // WritePluginConsole("Global chat called", "Info", 5);
            ProcessChatMessage(speaker, message);
        }

        public void OnRoundOver(int winningTeamId)
        {
            WritePluginConsole("^bRound Over!", "Info", 3);
            this.server.Midgame = false;

            if (enableRoundBalancer == State.Enabled)
            {
                this.ExecuteCommand("procon.protected.tasks.add", "taskFreeUpSlots", (roundTimeDelay - 5).ToString(), "1", "1", "procon.protected.plugins.call", "xEquilibrium", "FreeUpSlots");
                this.ExecuteCommand("procon.protected.tasks.add", "taskMoveOutOfSquads", (roundTimeDelay - 3).ToString(), "1", "1", "procon.protected.plugins.call", "xEquilibrium", "MoveOutOfSquads");
                this.ExecuteCommand("procon.protected.tasks.add", "taskRoundBalance", (roundTimeDelay).ToString(), "1", "1", "procon.protected.plugins.call", "xEquilibrium", "BalanceRound");

                this.ExecuteCommand("procon.protected.tasks.add", "taskTurnOffAutobalance", (roundTimeDelay).ToString(), "1", "1", "procon.protected.send", "vars.autoBalance", "false");
            }
        }

        public virtual void OnRoundOverTeamScores(List<TeamScore> teamScores)
        {
            if (server.PlayerCount > 30)
            {
                List<string> filedata = new List<string>();
                string file = "Map Bias Data.txt";

                try
                {
                    StreamReader sr = new StreamReader(file);
                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        filedata.Add(line);
                    }
                    sr.Close();
                }
                catch
                {

                }


                bool found = false;
                for (int i = 0; i < filedata.Count; i++)
                {
                    if (filedata[i].Contains(GetMapByFilename(this.server.CurrentMap).PublicLevelName) && filedata[i].Contains(this.server.CurrentMode))
                    {
                        filedata[i] += "(" + teamScores[0].Score + " " + teamScores[1].Score + ") | ";
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    filedata.Add(GetMapByFilename(this.server.CurrentMap).PublicLevelName + " | " + this.server.CurrentMode + " | (" + teamScores[0].Score + " " + teamScores[1].Score + ") | ");
                }

                StreamWriter sw = new StreamWriter(file);

                foreach (string txtLine in filedata)
                {
                    sw.WriteLine(txtLine);
                }

                sw.Close();
            }
        }

        public virtual void OnRoundOverPlayers(List<CPlayerInfo> lstPlayers)
        {

        }

        public void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal)
        {
            WritePluginConsole("Level loaded!", "Info", 3);
            WritePluginConsole(GetMapByFilename(mapFileName).PublicLevelName + " " + Gamemode, "Info", 3);
            this.ExecuteCommand("procon.protected.send", "serverInfo");
            roundStart = DateTime.Now;
            autobalanceExpire = DateTime.Now.AddSeconds(antiAutobalanceDuration);
            this.server.Midgame = true;
            nextMapShown = false;
            this.server.SwitchTargetTeams();
            this.server.SetEverybodyAlive(false);

            this.ExecuteCommand("procon.protected.tasks.add", "taskTurnOnAutobalance", antiAutobalanceDuration.ToString(), "1", "1", "procon.protected.send", "vars.autoBalance", "true");

            this.roundMap.Insert(0, GetMapByFilename(mapFileName).PublicLevelName + " " + Gamemode);
            this.roundScores.Insert(0, new List<int>());
            this.roundPlayerCount.Insert(0, new List<int>());
            this.roundDexterity.Insert(0, new List<int>());
        }

        public void OnRestartLevel()
        {
            this.server.Midgame = false;
        }

        public void OnRunNextLevel()
        {
            this.server.Midgame = false;
        }

        public void OnResponseError(List<string> lstRequestWords, string strError)
        {
            try
            {
                //admin.movePlayer Lesiu5 2 1 False " Reason: PlayerNotDead

                if (lstRequestWords[0] == "admin.movePlayer" && server.Contains(lstRequestWords[1]))
                {
                    string soldierName = lstRequestWords[1];

                    string tag = this.server[soldierName].Tag == "" ? "" : string.Format("[{0}]", this.server[soldierName].Tag);
                    string soldier = string.Format("{0} {1} ^9({2})^0", tag, soldierName, this.server[soldierName].Dexterity);
                    if (this.showTeamChanges == enumBoolYesNo.Yes || this.showTeamBalances == enumBoolYesNo.Yes)
                    {
                        WritePluginConsole(string.Format("AdminMove: ^6<{2} {3}> ^8^b=> ^6^n<{4} {5}> ^7{1} ^8[FAILED: {6}]", "", soldier, this.server[soldierName].TeamId, this.server[soldierName].SquadId, lstRequestWords[2], lstRequestWords[3], strError), "Warning", 2);
                    }

                    if (strError == "PlayerNotDead")
                    {
                        server[lstRequestWords[1]].Alive = true;
                        server[lstRequestWords[1]].Moves--;
                    }
                }
                else if (showFailedCommands == enumBoolYesNo.Yes)
                {
                    string command = "";
                    foreach (string word in lstRequestWords)
                    {
                        command += word + " ";
                    }

                    WritePluginConsole("Failed Command: \"" + command + "\" Reason: " + strError, "Warning", 2);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in OnResponseError", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        #endregion

        #region Procon Command Wrappers

        private void ProconMove(string name, int team, int squad, bool forcekill)
        {
            this.server[name].TargetTeam = team;
            this.server[name].TargetSquad = squad;
            this.ExecuteCommand("procon.protected.send", "admin.movePlayer", name, team.ToString(), squad.ToString(), forcekill.ToString());
        }

        #endregion

        #region JoinBalance

        /// <summary>
        /// Determines the best team for dexterity balance a specific player should join, then sets it as the target team.
        /// </summary>
        /// <param name="joinerName">Player to determine the best team for</param>
        private void JoinSetTargetTeam(string joinerName)
        {
            try
            {               
                int[] teamCount;
                int[] teamDexterity;
                int[] teamDexterityAdjusted;

                List<xPlayer> joiningPlayers = BalanceJoiners(out teamCount, out teamDexterity, out teamDexterityAdjusted);
                foreach (xPlayer player in joiningPlayers)
                {
                    if (joinerName == player.Name)
                    {
                        server[joinerName].TargetTeam = player.TargetTeam;
                        server[joinerName].TargetSquad = player.TargetSquad;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in JoinSetTargetTeam", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        /// <summary>
        /// Virtually places the joining players into each team so that the best dexterity balance is achieved.
        /// </summary>
        /// <param name="outTeamCount">The virtual team counts after all joining players have joined the determined teams.</param>
        /// <param name="outTeamDexterity">The virtual dexterity of the teams after all joining players have joined the determined teams.</param>
        /// <param name="outAdjustedTeamDexterity">The virtual adjusted (including winning team and map biases) dexterity of the teams after all joining players have joined the determined teams.</param>
        /// <returns>A list of the joining players</returns>
        private List<xPlayer> BalanceJoiners(out int[] outTeamCount, out int[] outTeamDexterity, out int[] outAdjustedTeamDexterity)
        {
            if (this.server != null)
            {
                try
                {
                    int[] teamDexterity = new int[2] { this.server.TeamDexterity(1), this.server.TeamDexterity(2) };
                    int[] teamDexterityAdjusted = new int[2] { this.server.TeamAdjustedDexterity(1), this.server.TeamAdjustedDexterity(2) };
                    int[] teamCount = new int[2] { this.server.TeamCount(1), this.server.TeamCount(2) };
                    int[] teamTickets = new int[2] { this.server.TeamScores[0].Score, this.server.TeamScores[1].Score };

                    // find low dex limit
                    List<xPlayer> tempPlayers = new List<xPlayer>(server.GetPlayers());
                    tempPlayers.Sort();
                    int lowDexterityLimit = 0;
                    if (tempPlayers.Count >= 4)
                    {
                        lowDexterityLimit = tempPlayers[(int)(tempPlayers.Count / 4)].Dexterity;
                    }

                    List<xPlayer> joiningPlayers = new List<xPlayer>(server.GetPlayers(0)); // players in team0
                    List<xPlayer> balancedJoiners = new List<xPlayer>();

                    // join player to team with friends
                    if (this.alwaysJoinFriends == enumBoolYesNo.Yes)
                    {
                        for (int i = 0; i < joiningPlayers.Count; i++)
                        {
                            List<xPlayer>[] friends = new List<xPlayer>[2];
                            friends[0] = GetFriends(server.GetPlayers(1), joiningPlayers[i]);
                            friends[1] = GetFriends(server.GetPlayers(2), joiningPlayers[i]);

                            if (friends[0].Count > 0 || friends[1].Count > 0)
                            {
                                int highDexterityTeam = 0;
                                if (teamDexterityAdjusted[0] < teamDexterityAdjusted[1])
                                {
                                    highDexterityTeam = 1;
                                }

                                int countDiff = Math.Abs(teamCount[0] - teamCount[1]);
                                int lowCountTeam = 1;
                                if (teamCount[0] < teamCount[1])
                                {
                                    lowCountTeam = 0;
                                }

                                int teamToJoin = lowCountTeam;

                                // friends on both teams
                                if (friends[0].Count > 0 && friends[1].Count > 0)
                                {
                                    if (joiningPlayers[i].Dexterity < lowDexterityLimit && teamCount[highDexterityTeam] < this.server.MaxPlayerCount / 2)
                                    {
                                        teamToJoin = highDexterityTeam;
                                    }
                                }
                                else if (friends[0].Count > 0 && teamCount[0] < this.server.MaxPlayerCount / 2)
                                {
                                    teamToJoin = 0;
                                }
                                else if (friends[1].Count > 0 && teamCount[1] < this.server.MaxPlayerCount / 2)
                                {
                                    teamToJoin = 1;
                                }

                                // find fullest squad with friends in it.
                                int squad = 0;
                                int squadCount = 0;
                                foreach (xPlayer friend in friends[teamToJoin])
                                { 
                                    int tempSquadCount = this.server.SquadCount(teamToJoin, friend.SquadId);
                                    if (tempSquadCount > squadCount && tempSquadCount < 4)
                                    {
                                        squadCount = tempSquadCount;
                                        squad = friend.SquadId;
                                    }
                                }

                                joiningPlayers[i].TargetTeam = teamToJoin + 1;
                                joiningPlayers[i].TargetSquad = squad;
                                teamDexterityAdjusted[teamToJoin] += joiningPlayers[i].Dexterity;
                                teamDexterity[teamToJoin] += joiningPlayers[i].Dexterity;
                                teamCount[teamToJoin]++;

                                balancedJoiners.Add(joiningPlayers[i]);
                                joiningPlayers.Remove(joiningPlayers[i]);
                            }
                        }
                    }

                    joiningPlayers.Sort();      // low to high skill

                    // join players without friends
                    while (joiningPlayers.Count > 0)
                    {
                        int highestDexterityPlayer = joiningPlayers.Count - 1;
                        int lowestDexterityPlayer = 0;

                        int highDexterityTeam = 0;
                        int lowDexterityTeam = 1;
                        if (teamDexterityAdjusted[0] < teamDexterityAdjusted[1])
                        {
                            highDexterityTeam = 1;
                            lowDexterityTeam = 0;
                        }

                        int countDiff = Math.Abs(teamCount[0] - teamCount[1]);
                        int lowCountTeam = 1;
                        if (teamCount[0] < teamCount[1])
                        {
                            lowCountTeam = 0;
                        }

                        //====================
                        // Join Logic
                        //====================

                        // default join settings
                        int playerToMove = highestDexterityPlayer;
                        int teamToJoin = lowCountTeam;

                        if (countDiff <= 1 && joiningPlayers[highestDexterityPlayer].Skill < lowDexterityLimit)
                        {
                            teamToJoin = highDexterityTeam;
                        }
                        else if (teamCount[0] == teamCount[1])
                        {
                            teamToJoin = lowDexterityTeam;
                        }
                        else if (lowCountTeam == highDexterityTeam) // and more than 2 joining
                        {
                            playerToMove = lowestDexterityPlayer;
                        }

                        // if team is already full
                        if (teamCount[teamToJoin] == this.server.MaxPlayerCount / 2)
                        {
                            if (teamToJoin == 0)
                            {
                                teamToJoin = 1;
                            }
                            else
                            {
                                teamToJoin = 0;
                            }
                        }

                        joiningPlayers[playerToMove].TargetTeam = teamToJoin + 1;
                        teamDexterityAdjusted[teamToJoin] += joiningPlayers[playerToMove].Dexterity;
                        teamDexterity[teamToJoin] += joiningPlayers[playerToMove].Dexterity;
                        teamCount[teamToJoin]++;

                        balancedJoiners.Add(joiningPlayers[playerToMove]);
                        joiningPlayers.Remove(joiningPlayers[playerToMove]);
                    }
                    outTeamCount = teamCount;
                    outTeamDexterity = teamDexterity;
                    outAdjustedTeamDexterity = teamDexterityAdjusted;

                    return balancedJoiners;
                }
                catch (Exception e)
                {
                    WritePluginConsole("Exception Caught in BalanceJoiners", "Error", 1);
                    WritePluginConsole(e.Message, "Error", 1);
                }
            }
            outTeamCount = new int[2];
            outTeamDexterity = new int[2];
            outAdjustedTeamDexterity = new int[2];

            return new List<xPlayer>();
        }

        #endregion
 
        #region LiveBalance

        /// <summary>
        /// Runs the balancing algorithm to determine a list of players that should be move if needed.
        /// </summary>
        /// <param name="waitTillDead">Wait till the players are dead or force move now?</param>
        /// <param name="perfectBalance">Keep balancing players until the best possible balance?</param>
        public void LiveBalance(string strWaitTillDead, string strPerfectBalance)
        {
            try
            {
                bool waitTillDead = true;
                bool.TryParse(strWaitTillDead, out waitTillDead);

                bool perfectBalance = false;
                bool.TryParse(strPerfectBalance, out perfectBalance);

                // clear player move list
                this.playersToMove = new List<xPlayer>();

                if (this.enableLiveBalancer == State.Enabled && this.server.GamemodeSupported && this.server.Midgame)
                {
                    int[] futureTeamCount;
                    int[] futureTeamDexterityAdjusted;
                    int[] futureTeamDexterity;

                    List<xPlayer> joiners = BalanceJoiners(out futureTeamCount, out futureTeamDexterity, out futureTeamDexterityAdjusted);

                    int proTeam = 0;
                    int noobTeam = 1;
                    if (futureTeamDexterityAdjusted[0] < futureTeamDexterityAdjusted[1])
                    {
                        proTeam = 1;
                        noobTeam = 0;
                    }
                    int dexterityDiff = futureTeamDexterityAdjusted[proTeam] - futureTeamDexterityAdjusted[noobTeam];

                    int aveDexterity = 200;
                    if (futureTeamCount[proTeam] + futureTeamCount[noobTeam] != 0)
                    {
                        aveDexterity = (futureTeamDexterityAdjusted[proTeam] + futureTeamDexterityAdjusted[noobTeam]) / (futureTeamCount[proTeam] + futureTeamCount[noobTeam]);
                    }

                    int highCountTeam = 0;
                    int lowCountTeam = 1;
                    if (futureTeamCount[0] < futureTeamCount[1])
                    {
                        highCountTeam = 1;
                        lowCountTeam = 0;
                    }
                    int countDiff = futureTeamCount[highCountTeam] - futureTeamCount[lowCountTeam];

                    int dexterityDiffThres = this.futureDexDiffWaitThreshold;
                    int countDiffThres = this.futurePlayerCountDiffWaitThreshold;
                    //if (perfectBalance)
                    //{
                    //    dexterityDiffThres = 500;
                    //    countDiffThres = 1;
                    //}



                    BalanceState balanceState = BalanceState.Balanced;

                    if (this.server.EnableLiveBalance)
                    {

                        string liveBalanceStatus = "";
                        // In normal ranges
                        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        if (countDiff <= countDiffThres && dexterityDiff <= dexterityDiffThres)
                        {
                            liveBalanceStatus = "LiveBalance: In normal ranges";
                            balanceState = BalanceState.Balanced;
                        }
                        // Stacked, but server full
                        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        else if ((countDiff > countDiffThres || dexterityDiff > dexterityDiffThres) && (server.GetPlayers(1).Count + server.GetPlayers(2).Count) == this.server.MaxPlayerCount)
                        {
                            liveBalanceStatus = "LiveBalance: ^3Stack detected, but server is full^0. Nothing can be done...";
                            balanceState = BalanceState.Stacked;
                        }
                        // Move pro to low count team
                        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        else if (countDiff >= countDiffThres && dexterityDiff > dexterityDiffThres && proTeam == highCountTeam)
                        {
                            liveBalanceStatus = "LiveBalance: ^3Count and dexterity stack detected!^0 Moving pros...";
                            balanceState = BalanceState.Stacked;

                            // sort pro team by skill.
                            List<xPlayer> proTeamPlayers = server.GetPlayers(proTeam + 1);

                            proTeamPlayers.Sort(delegate(xPlayer player1, xPlayer player2)
                            {
                                return player2.Dexterity.CompareTo(player1.Dexterity);
                            });

                            // move top 1/3 players without friends on pro team
                            for (int i = 0; i <= proTeamPlayers.Count / 3; i++)
                            {
                                List<xPlayer> friendsOnThisTeam = new List<xPlayer>(GetFriends(proTeamPlayers, proTeamPlayers[i]));
                                if (friendsOnThisTeam.Count == 0 && proTeamPlayers[i].Moves < this.playerMoveLimit && !proTeamPlayers[i].Whitelisted)
                                {
                                    proTeamPlayers[i].TargetTeam = lowCountTeam + 1;
                                    proTeamPlayers[i].TargetSquad = 0;
                                    this.playersToMove.Add(proTeamPlayers[i]);
                                }
                            }
                        }
                        // Move noob to low count team
                        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        else if (countDiff > countDiffThres && dexterityDiff > dexterityDiffThres && noobTeam == highCountTeam)
                        {
                            liveBalanceStatus = "LiveBalance: ^3Count and dexterity stack detected!^0 Moving noobs...";
                            balanceState = BalanceState.Stacked;

                            
                            List<xPlayer> noobTeamPlayers = server.GetPlayers(noobTeam + 1);

                            // sort noob team by skill.
                            noobTeamPlayers.Sort(delegate(xPlayer player1, xPlayer player2)
                            {
                                return player1.Dexterity.CompareTo(player2.Dexterity);
                            });

                            // move bottom 1/3 players without friends on noob team
                            for (int i = 0; i <= noobTeamPlayers.Count / 3; i++)
                            {
                                List<xPlayer> friendsOnThisTeam = new List<xPlayer>(GetFriends(noobTeamPlayers, noobTeamPlayers[i]));

                                if (friendsOnThisTeam.Count == 0 && noobTeamPlayers[i].Moves < this.playerMoveLimit && !noobTeamPlayers[i].Whitelisted)
                                {
                                    noobTeamPlayers[i].TargetTeam = lowCountTeam + 1;
                                    noobTeamPlayers[i].TargetSquad = 0;
                                    this.playersToMove.Add(noobTeamPlayers[i]);
                                }
                            }
                        }
                        // Swap a pro with a noob
                        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        else if (countDiff < countDiffThres && dexterityDiff > dexterityDiffThres)
                        {
                            liveBalanceStatus = "LiveBalance: ^3Dexterity stack detected!^0 Swapping pros with noobs...";
                            balanceState = BalanceState.Stacked;

                            List<xPlayer> proTeamPlayers = server.GetPlayers(proTeam + 1);
                            List<xPlayer> noobTeamPlayers = server.GetPlayers(noobTeam + 1);

                            proTeamPlayers.Sort(delegate(xPlayer player1, xPlayer player2)
                            {
                                return player2.Dexterity.CompareTo(player1.Dexterity);
                            });

                            noobTeamPlayers.Sort(delegate(xPlayer player1, xPlayer player2)
                            {
                                return player1.Dexterity.CompareTo(player2.Dexterity);
                            });

                            // Move top 1/3 players without friends on pro team if there is space on noob team
                            for (int i = 0; i <= proTeamPlayers.Count / 3 && noobTeamPlayers.Count < this.server.MaxPlayerCount / 2; i++)
                            {
                                List<xPlayer> friendsOnThisTeam = new List<xPlayer>(GetFriends(proTeamPlayers, proTeamPlayers[i]));

                                if (friendsOnThisTeam.Count == 0 && proTeamPlayers[i].Moves < this.playerMoveLimit && !proTeamPlayers[i].Whitelisted)
                                {
                                    proTeamPlayers[i].TargetTeam = noobTeam + 1;
                                    proTeamPlayers[i].TargetSquad = 0;
                                    this.playersToMove.Add(proTeamPlayers[i]);
                                }
                            }

                            // Move bottom 1/3 players without friends on noob team if there is space on pro team
                            for (int i = 0; i <= noobTeamPlayers.Count / 3 && proTeamPlayers.Count < this.server.MaxPlayerCount / 2; i++)
                            {
                                List<xPlayer> friendsOnThisTeam = new List<xPlayer>(GetFriends(noobTeamPlayers, noobTeamPlayers[i]));

                                if (friendsOnThisTeam.Count == 0 && noobTeamPlayers[i].Moves < this.playerMoveLimit && !noobTeamPlayers[i].Whitelisted)
                                {
                                    noobTeamPlayers[i].TargetTeam = proTeam + 1;
                                    noobTeamPlayers[i].TargetSquad = 0;
                                    this.playersToMove.Add(noobTeamPlayers[i]);
                                }
                            }

                            // Move average player to low count team
                            //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        }
                        else if (countDiff > countDiffThres && dexterityDiff <= this.futureDexDiffWaitThreshold)
                        {
                            liveBalanceStatus = "LiveBalance: ^3Count stack detected!^0 Moving average players...";
                            balanceState = BalanceState.Stacked;

                            List<xPlayer> highCountPlayers = server.GetPlayers(highCountTeam + 1);

                            highCountPlayers.Sort(delegate(xPlayer player1, xPlayer player2)
                            {
                                return player1.Skill.CompareTo(player2.Skill);
                            });

                            int start = Convert.ToInt32((double)highCountPlayers.Count * 1.0 / 3.0);
                            int end = Convert.ToInt32((double)highCountPlayers.Count * 2.0 / 3.0);
                            for (int i = start; i <= end; i++)
                            {
                                List<xPlayer> friendsOnThisTeam = new List<xPlayer>(GetFriends(highCountPlayers, highCountPlayers[i]));

                                if (friendsOnThisTeam.Count == 0 && highCountPlayers[i].Moves < this.playerMoveLimit && !highCountPlayers[i].Whitelisted)
                                {
                                    highCountPlayers[i].TargetTeam = lowCountTeam + 1;
                                    highCountPlayers[i].TargetSquad = 0;
                                    this.playersToMove.Add(highCountPlayers[i]);
                                }
                            }
                        }

                        if (showLiveMonitor == enumBoolYesNo.Yes && (previousBState != balanceState || balanceState != BalanceState.Balanced))
                        {
                            if (showLiveMonitor == enumBoolYesNo.Yes)
                            {
                                WritePluginConsole("------------------------------------------------------", "Info", 4);
                                if (perfectBalance)
                                {
                                    WritePluginConsole("LiveBalance: PerfectBalance: ENABLED", "Info", 4);
                                }
                                WritePluginConsole("LiveBalance: Current:    Count: " + string.Format("^4{0,2}^0", server.TeamCount(1)) + " v " + string.Format("^4{0,-2}^0", server.TeamCount(2)) + " ^9(" + string.Format("{0,2:+#;-#;0}", server.TeamCount(1) - server.TeamCount(2)) +
                                                                 ")^0    Dexterity: " + string.Format("^4{0,4}^0", server.TeamDexterity(1)) + " v " + string.Format("^4{0,-4}^0", server.TeamDexterity(2)) + " ^9(" + string.Format("{0,2:+#;-#;0}", server.TeamDexterity(1) - server.TeamDexterity(2)) +
                                                               ")^0    AdjustedDex: " + string.Format("^4{0,4}^0", server.TeamAdjustedDexterity(1)) + " v " + string.Format("^4{0,-4}^0", server.TeamAdjustedDexterity(2)) + " ^9(" + string.Format("{0,2:+#;-#;0}", server.TeamAdjustedDexterity(1) - server.TeamAdjustedDexterity(2)) +
                                                                      ")^0   Score: " + string.Format("^4{0,3}^0", this.server.TeamScores[0].Score) + " v " + string.Format("^4{0,-3}^0", this.server.TeamScores[1].Score) + " ^9(" + string.Format("{0,2:+#;-#;0}", this.server.TeamScores[0].Score - this.server.TeamScores[1].Score) + ")^0", "Info", 4);
                                WritePluginConsole("LiveBalance: Future  :    Count: " + string.Format("^4{0,2}^0", futureTeamCount[0]) + " v " + string.Format("^4{0,-2}^0", futureTeamCount[1]) + " ^9(" + string.Format("{0,2:+#;-#;0}", futureTeamCount[0] - futureTeamCount[1]) +
                                                                  ")^0    Dexterity: " + string.Format("^4{0,4}^0", futureTeamDexterity[0]) + " v " + string.Format("^4{0,-4}^0", futureTeamDexterity[1]) + " ^9(" + string.Format("{0,2:+#;-#;0}", futureTeamDexterity[0] - futureTeamDexterity[1]) +
                                                                ")^0    AdjustedDex: " + string.Format("^4{0,4}^0", futureTeamDexterityAdjusted[0]) + " v " + string.Format("^4{0,-4}^0", futureTeamDexterityAdjusted[1]) + " ^9(" + string.Format("{0,2:+#;-#;0}", futureTeamDexterityAdjusted[0] - futureTeamDexterityAdjusted[1]) + ")^0", "Info", 4);
                            }
                            WritePluginConsole(liveBalanceStatus, "Info", 3);
                            WritePluginConsole("------------------------------------------------------", "Info", 4);
                        }
                        previousBState = balanceState;

                        //==============================
                        // Save Round Statistics
                        //==============================

                        if (this.roundScores.Count > 0)
                        {
                            this.roundScores[0].Add(this.server.TeamScores[0].Score - server.TeamScores[1].Score);
                            this.roundPlayerCount[0].Add(this.server.TeamCount(1) - server.TeamCount(2));
                            this.roundDexterity[0].Add(this.server.TeamDexterity(1) - server.TeamDexterity(2));
                        }


                        // sort highest to lowest
                        this.playersToMove.Sort(delegate(xPlayer player1, xPlayer player2)
                        {
                            return player2.Dexterity.CompareTo(player1.Dexterity);
                        });

                        // output players to move
                        if (this.playersToMove.Count > 0)
                        {
                            string line = "^7";
                            foreach (xPlayer player in this.playersToMove)
                            {
                                line += player.Name + " ^9(" + player.Dexterity + ") ^0| ^7";
                            }
                            if (showLiveMonitor == enumBoolYesNo.Yes)
                            {
                                WritePluginConsole("LiveBalance: Players to move: " + line.Substring(0, line.Length - 4), "Info", 4);
                            }
                        }
                        else if (liveBalanceStatus.CompareTo("LiveBalance: In normal ranges") != 0)
                        {
                            WritePluginConsole("LiveBalance: No one to move =(", "Info", 4);
                        }

                        // check if any of the players can be moved now
                        for (int i = 0; i < this.playersToMove.Count; i++)
                        {
                            if (!this.playersToMove[i].Alive)
                            {
                                int squadId = server.GetFullestSquadId(this.playersToMove[i].TargetTeam);
                                ProconMove(this.playersToMove[i].Name, this.playersToMove[i].TargetTeam, squadId, false);
                                if (DateTime.Now > this.autobalanceExpire && this.server.Midgame)
                                {
                                    server[this.playersToMove[i].Name].Moves++;
                                }
                                if (showTeamBalances == enumBoolYesNo.Yes)
                                {
                                    WritePluginConsole("^7" + this.playersToMove[i].Name + "^0 is already dead, using to balance team " + this.playersToMove[i].TargetTeam + ".", "Info", 3);
                                }
                                this.playersToMove = new List<xPlayer>();
                                //if (perfectBalance)
                                //{
                                //    this.ExecuteCommand("procon.protected.tasks.add", "taskLiveBalance1", "1", "1", "1", "procon.protected.plugins.call", GetPluginName(), "LiveBalance", "true", "true");
                                //}
                                //else
                                //{
                                    this.ExecuteCommand("procon.protected.tasks.add", "taskLiveBalance1", "1", "1", "1", "procon.protected.plugins.call", GetPluginName(), "LiveBalance", "true", "false");
                                //}
                                break;
                            }
                        }
                    }
                    else if (showLiveMonitor == enumBoolYesNo.Yes && (countDiff > countDiffThres || dexterityDiff > dexterityDiffThres))
                    {
                        WritePluginConsole("LiveBalance: Stack detected, but ticket count less than theshold. No action will be taken.", "Info", 3);
                    }
                }
                else
                {
                    WritePluginConsole("LiveBalance: Disabled", "Info", 5);
                }
              //  this.ExecuteCommand("procon.protected.send", "serverinfo");
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in LiveBalance", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }
        
        /// <summary>
        /// Checks each player who has recently moved to compile infomation about that move as it is triggered. Once the time limit has passed, it outputs the move summary and decides if
        /// that player is allowed to move to their current team or not.
        /// </summary>
        public void ProcessTeamChanges()
        {
            try
            {
                lock (teamChangesLock)
                {
                    for (int i = 0; i < this.teamChanges.Count; i++)
                    {
                        if (this.teamChanges[i].Expired || this.teamChanges[i].AdminMoved)
                        {
                            string soldierName = this.teamChanges[i].Name;
                            string tag = this.server[soldierName].Tag == "" ? "" : string.Format("[{0}]", this.server[soldierName].Tag);
                            string soldier = string.Format("{0} {1} ^9({2})^0", tag, soldierName, this.server[soldierName].Dexterity);
                            if (this.teamChanges[i].AdminMoved && ((showTeamChanges == enumBoolYesNo.Yes && this.teamChanges[i].EndTeam != this.teamChanges[i].StartTeam) || (showSquadChanges == enumBoolYesNo.Yes && this.teamChanges[i].EndTeam == this.teamChanges[i].StartTeam)))
                            {
                                WritePluginConsole(string.Format("AdminMove: ^6<{2} {3}> ^2^b=> ^6^n<{4} {5}> ^7{1}", "", soldier, this.teamChanges[i].StartTeam, this.teamChanges[i].StartSquad, this.teamChanges[i].EndTeam, this.teamChanges[i].EndSquad), "Info", 3);
                            }
                            else if ((showTeamChanges == enumBoolYesNo.Yes && this.teamChanges[i].EndTeam != this.teamChanges[i].StartTeam) || (showSquadChanges == enumBoolYesNo.Yes && this.teamChanges[i].EndTeam == this.teamChanges[i].StartTeam))
                            {
                                WritePluginConsole(string.Format("PlayerMove : ^6<{2} {3}> ^2^b=> ^6^n<{4} {5}> ^7{1}", "", soldier, this.teamChanges[i].StartTeam, this.teamChanges[i].StartSquad, this.teamChanges[i].EndTeam, this.teamChanges[i].EndSquad), "Info", 3);
                            }

                            if (this.teamChanges[i].EndTeam != this.server[soldierName].TargetTeam && this.server.Midgame)
                            {
                                if (allowTeamChanges == RState.Allowed)
                                {
                                    WritePluginConsole("^7" + soldierName + "^0 is on team ^6" + this.teamChanges[i].EndTeam + "^0 but target team is ^6" + this.server[soldierName].TargetTeam + "^0. Allowing...", "Info", 4);
                                    this.server[soldierName].TargetTeam = this.teamChanges[i].EndTeam;
                                }
                                else if (allowTeamChanges == RState.Restricted)
                                {
                                    int currentTeam = this.teamChanges[i].EndTeam;
                                    int otherTeam = this.server[soldierName].TargetTeam;

                                    if (this.server[soldierName].Whitelisted || this.teamChanges[i].AdminMoved || (this.server.TeamAdjustedDexterity(currentTeam) - this.server[soldierName].Dexterity < this.server.TeamAdjustedDexterity(otherTeam) && this.server.TeamCount(currentTeam) - 1 <= this.server.TeamCount(otherTeam)))
                                    {
                                        // allow
                                        WritePluginConsole("^7" + soldierName + "^0 is on team ^6" + this.teamChanges[i].EndTeam + "^0 but target team is ^6" + this.server[soldierName].TargetTeam + "^0. Allowing...", "Info", 4);
                                        this.server[soldierName].TargetTeam = this.teamChanges[i].EndTeam;
                                    }
                                    else
                                    {
                                        // disallow, move back
                                        WritePluginConsole("^7" + soldierName + "^0 was moved back to ^6" + this.server[soldierName].TargetTeam + "^0 for stacking.", "Work", 4);
                                        ProconMove(soldierName, this.server[soldierName].TargetTeam, this.server[soldierName].TargetSquad, true);
                                    }
                                }
                                else if (allowTeamChanges == RState.Disallowed)
                                {
                                    WritePluginConsole("^7" + soldierName + "^0 is on team ^6" + this.teamChanges[i].EndTeam + "^0 but target team is ^6" + this.server[soldierName].TargetTeam + ". Moving back to " + this.server[soldierName].TargetTeam, "Work", 4);
                                    ProconMove(soldierName, this.server[soldierName].TargetTeam, this.server[soldierName].TargetSquad, true);
                                }
                            }

                            this.teamChanges.RemoveAt(i);
                            i--;
                        }
                    }

                    this.ExecuteCommand("procon.protected.tasks.remove", "taskProcessTeamChanges");
                    if (this.teamChanges.Count != 0)
                    {
                        this.ExecuteCommand("procon.protected.tasks.add", "taskProcessTeamChanges", "1", "1", "-1", "procon.protected.plugins.call", GetPluginName(), "ProcessTeamChanges");
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in ProcessTeamChanges", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        #endregion

        #region RoundBalance

        /// <summary>
        /// Rebalances each teams dexterixy and groups friends together.
        /// </summary>
        public void BalanceRound()
        {
            try
            {
                WritePluginConsole("BalanceRound starting...", "Info", 4);
                // find players in server
                List<xPlayer> allPlayers = new List<xPlayer>();

                lock (teamChangesLock)
                {
                    foreach (xPlayer player in server)
                    {
                        allPlayers.Add(player);
                    }
                }
                List<List<xPlayer>> clusters = new List<List<xPlayer>>(ClusterizeFriendTree(allPlayers));

                // reduce cluster sizes to at most half the people on the server
                for (int i = 0; i < clusters.Count; i++)
                {
                    while (clusters[i].Count > server.PlayerCount / 2 || clusters[i].Count > this.splitFriendsThreshold)
                    {
                        StreamWriter sw = new StreamWriter(String.Format("./Logs/{0}_{1}/{2:yyyyMMdd}_PlayerInformation.log", m_strHostName, m_strPort, DateTime.Now), true);
                        sw.WriteLine("Cluster count (" + clusters[i].Count + ") is over server limit (" + server.PlayerCount / 2 + " or " + this.splitFriendsThreshold + ") fracturing...");
                        sw.Close();

                        List<xPlayer>[] smallerClusters = FractureCluster(clusters[i]);
                        if (smallerClusters[0].Count == 0 && smallerClusters[1].Count != 0)
                        {
                            clusters[i] = new List<xPlayer>(smallerClusters[1]);
                        }
                        else if (smallerClusters[1].Count == 0 && smallerClusters[0].Count != 0)
                        {
                            clusters[i] = new List<xPlayer>(smallerClusters[0]);
                        }
                        else
                        {
                            clusters[i] = new List<xPlayer>(smallerClusters[0]);
                            clusters.Add(smallerClusters[1]);
                        }
                    }
                }
                WritePluginConsole("Massive clusters split.", "Info", 5);

                // sort by cluster skill
                clusters.Sort(delegate(List<xPlayer> list1, List<xPlayer> list2)
                {
                    int dexterity1 = SumDexterityofPlayers(list1);
                    int dexterity2 = SumDexterityofPlayers(list2);
                    return dexterity2.CompareTo(dexterity1);
                });

                WritePluginConsole("Clusters sorted by dexterity.", "Info", 5);

                List<List<xPlayer>>[] team = new List<List<xPlayer>>[2];
                team[0] = new List<List<xPlayer>>();
                team[1] = new List<List<xPlayer>>();

                alternateTeams = (alternateTeams + 1) % 2;

                while (clusters.Count > 0)
                {
                    int[] teamDexterity = new int[2];
                    int[] teamCount = new int[2];

                    teamDexterity[0] = 0;
                    teamCount[0] = 0;
                    foreach (List<xPlayer> players in team[0])
                    {
                        teamDexterity[0] += SumDexterityofPlayers(players);
                        teamCount[0] += players.Count;
                    }

                    teamDexterity[1] = 0;
                    teamCount[1] = 0;
                    foreach (List<xPlayer> players in team[1])
                    {
                        teamDexterity[1] += SumDexterityofPlayers(players);
                        teamCount[1] += players.Count;
                    }

                    // Change the team to start filling each round
                    teamDexterity[alternateTeams]++;

                    int highestDexterityCluster = 0;
                    int lowestDexterityCluster = clusters.Count - 1;

                    int highDexterityTeam = 0;

                    if (teamDexterity[0] < teamDexterity[1])
                    {
                        highDexterityTeam = 1;
                    }

                    int lowCountTeam = 1;
                    if (teamCount[0] < teamCount[1])
                    {
                        lowCountTeam = 0;
                    }

                    int clusterToMove = highestDexterityCluster;
                    int teamToJoin = lowCountTeam;

                    if (lowCountTeam == highDexterityTeam)
                    {
                        clusterToMove = lowestDexterityCluster;
                    }

                    team[teamToJoin].Add(clusters[clusterToMove]);
                    clusters.RemoveAt(clusterToMove);
                }

                WritePluginConsole("Split clusters into even teams.", "Info", 5);

                StreamWriter sWriter = new StreamWriter(String.Format("./Logs/{0}_{1}/{2:yyyyMMdd}_PlayerInformation.log", m_strHostName, m_strPort, DateTime.Now), true);

                int totalDexterity = 0;
                sWriter.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
                sWriter.WriteLine("===========");
                sWriter.WriteLine(" TEAM 0 ");
                sWriter.WriteLine("===========");
                foreach (List<xPlayer> cluster in team[0])
                {
                    string line = "Cluster: ";
                    foreach (xPlayer player in cluster)
                    {
                        line += player.Name + " ";
                    }
                    line += "\t" + SumDexterityofPlayers(cluster);
                    totalDexterity += SumDexterityofPlayers(cluster);
                    sWriter.WriteLine(line);
                }
                sWriter.WriteLine("___________");
                sWriter.WriteLine("TOTAL DEXTERITY " + totalDexterity);
                sWriter.WriteLine("");

                totalDexterity = 0;
                sWriter.WriteLine("===========");
                sWriter.WriteLine(" TEAM 1 ");
                sWriter.WriteLine("===========");
                foreach (List<xPlayer> cluster in team[1])
                {
                    string line = "Cluster: ";
                    foreach (xPlayer player in cluster)
                    {
                        line += player.Name + " ";
                    }
                    line += "\t" + SumDexterityofPlayers(cluster);
                    totalDexterity += SumDexterityofPlayers(cluster);
                    sWriter.WriteLine(line);
                }
                sWriter.WriteLine("___________");
                sWriter.WriteLine("TOTAL DEXTERITY " + totalDexterity);
                sWriter.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
                sWriter.Close();

                // reduce cluster sizes to squad sizes on both sides
                foreach (List<List<xPlayer>> side in team)
                {
                    for (int i = 0; i < side.Count; i++)
                    {
                        while (side[i].Count > 4)
                        {
                            List<xPlayer>[] smallerClusters = FractureCluster(side[i]);
                            side[i] = new List<xPlayer>(smallerClusters[0]);
                            side.Add(smallerClusters[1]);
                        }
                    }
                }
                WritePluginConsole("Reduced cluster sizes", "Info", 5);

                // condense squads
                foreach (List<List<xPlayer>> side in team)
                {
                    side.Sort(delegate(List<xPlayer> list1, List<xPlayer> list2)
                    {
                        return list2.Count.CompareTo(list1.Count);
                    });

                    for (int i = 0; i < side.Count; i++)
                    {
                        if (side[i].Count == 4)
                        {
                            // do nothing
                        }
                        else
                        {
                            for (int j = side.Count - 1; j > i; j--)
                            {
                                if (side[j].Count <= 4 - side[i].Count)
                                {
                                    foreach (xPlayer player in side[j])
                                    {
                                        side[i].Add(player);
                                    }
                                    side.RemoveAt(j);
                                    i--;
                                    break;
                                }
                            }
                        }
                    }
                }

                WritePluginConsole("Condensed squads", "Info", 5);

                sWriter = new StreamWriter("friends.txt", true);

                totalDexterity = 0;
                sWriter.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
                sWriter.WriteLine("===========");
                sWriter.WriteLine(" TEAM 0 ");
                sWriter.WriteLine("===========");
                foreach (List<xPlayer> cluster in team[0])
                {
                    string line = "Cluster: ";
                    foreach (xPlayer player in cluster)
                    {
                        line += player.Name + " ";
                    }
                    line += "\t" + SumDexterityofPlayers(cluster);
                    totalDexterity += SumDexterityofPlayers(cluster);
                    sWriter.WriteLine(line);
                }
                sWriter.WriteLine("___________");
                sWriter.WriteLine("TOTAL DEXTERITY " + totalDexterity);
                sWriter.WriteLine("");

                totalDexterity = 0;
                sWriter.WriteLine("===========");
                sWriter.WriteLine(" TEAM 1 ");
                sWriter.WriteLine("===========");
                foreach (List<xPlayer> cluster in team[1])
                {
                    string line = "Cluster: ";
                    foreach (xPlayer player in cluster)
                    {
                        line += player.Name + " ";
                    }
                    line += "\t" + SumDexterityofPlayers(cluster);
                    totalDexterity += SumDexterityofPlayers(cluster);
                    sWriter.WriteLine(line);
                }
                sWriter.WriteLine("___________");
                sWriter.WriteLine("TOTAL DEXTERITY " + totalDexterity);
                sWriter.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
                sWriter.Close();

                // assign target positions
                for (int k = 0; k < 2; k++)
                {
                    for (int i = 0; i < team[k].Count; i++)
                    {
                        for (int j = 0; j < team[k][i].Count; j++)
                        {
                            team[k][i][j].TargetTeam = k + 1;
                            team[k][i][j].TargetSquad = i + 1;
                        }
                    }
                }
                WritePluginConsole("Target team/squads assigned.", "Info", 5);

                this.ExecuteCommand("procon.protected.tasks.add", "taskTryMoveToTargetSquads", "2", "1", "1", "procon.protected.plugins.call", "xEquilibrium", "TryMoveToTargetSquads");
             //   this.ExecuteCommand("procon.protected.tasks.add", "taskTryMoveTargetSquads", "0", "3", "3", "procon.protected.plugins.call", "xEquilibrium", "MoveToPlayersToTarget");

                WritePluginConsole("BalanceRound Done!", "Info", 3);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in BalanceRound", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
            
        }

        /// <summary>
        /// Moves each player on the server out of a squad.
        /// </summary>
        public void MoveOutOfSquads()
        {
            try
            {
                // make temp copy of server
                List<xPlayer> players = new List<xPlayer>(this.server.GetPlayers());

                // move out of squads
                foreach (xPlayer player in players)
                {
                    if (player.TeamId != 0)
                    {
                        ProconMove(player.Name, player.TeamId, 0, true);
                        WritePluginConsole(string.Format("MoveOutOfSquads: ^7{0,-15} ^6<{1} {2}>^0 -> ^6<{3} {4}>", player.Name, player.TeamId, player.SquadId, player.TeamId, 0), "Info", 4);
                    }
                }

                WritePluginConsole("Moved everyone out of the squads.", "Info", 4);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in MoveOutOfSquads", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        ///// <summary>
        ///// Trys to move each player in the server to their assigned target team.
        ///// </summary>
        //public void TryMoveToTargetTeam()
        //{
        //    try
        //    {
        //        WritePluginConsole("Trying to move players to target teams now!", "Work", 3);

        //        List<xPlayer> players = this.server.GetPlayers();

        //        foreach (xPlayer player in players)
        //        {
        //            WritePluginConsole(string.Format("^7{0,-15}^0 Current: ^6<{1} {2}>^0 Target: ^6<{3} {4}>", player.Name, player.TeamId, player.SquadId, player.TargetTeam, 0), "Info", 4);
        //        }

        //        List<xPlayer>[] toMove = new List<xPlayer>[2];
        //        toMove[0] = new List<xPlayer>();
        //        toMove[1] = new List<xPlayer>();

        //        // players to just move out of squads
        //        for (int i = 0; i < players.Count; i++)
        //        {
        //            if (players[i].TeamId == players[i].TargetTeam && players[i].TargetTeam == 1 && players[i].TeamId != 0)
        //            {
        //                toMove[0].Add(players[i]);
        //            }
        //            else if (players[i].TeamId == players[i].TargetTeam && players[i].TargetTeam == 2 && players[i].TeamId != 0)
        //            {
        //                toMove[1].Add(players[i]);
        //            }
        //        }
        //        // players needing to be on the other team
        //        for (int i = 0; i < players.Count; i++)
        //        {
        //            if (players[i].TeamId != players[i].TargetTeam && players[i].TargetTeam == 1 && players[i].TeamId != 0)
        //            {
        //                toMove[0].Add(players[i]);
        //            }
        //            else if (players[i].TeamId != players[i].TargetTeam && players[i].TargetTeam == 2 && players[i].TeamId != 0)
        //            {
        //                toMove[1].Add(players[i]);
        //            }
        //        }

        //        WritePluginConsole("==================================", "Info", 4);
        //        WritePluginConsole("Moving to Team 1: " + toMove[0].Count + " | Moving to Team 2: " + toMove[1].Count, "Info", 4);
        //        WritePluginConsole("==================================", "Info", 4);

        //        int k = 0;
        //        if (this.server.TeamCount(1) > this.server.TeamCount(2))
        //        {
        //            k = 1;
        //        }

        //        while (toMove[0].Count > 0 || toMove[1].Count > 0)
        //        {
        //            if (toMove[k].Count > 0)
        //            {
        //                ProconMove(toMove[k][0].Name, toMove[k][0].TargetTeam, 0, true);
        //                WritePluginConsole(string.Format("RoundBalance: ^7[{0,4}] {1,-15} ^6<{2} {3}> -> ^6<{4} {5}>", toMove[k][0].Tag, toMove[k][0].Name, toMove[k][0].TeamId, toMove[k][0].SquadId, toMove[k][0].TargetTeam, 0), "Info", 4);
        //                toMove[k].RemoveAt(0);
        //            }
        //            k = (k + 1) % 2;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        WritePluginConsole("Exception caught in TryMoveToTargetTeam", "Error", 1);
        //        WritePluginConsole(e.Message, "Error", 1);
        //    }
        //}

        /// <summary>
        /// Trys to move each player in the server to their assigned target team/squad.
        /// </summary>
        public void TryMoveToTargetSquads()
        {
            try
            {
                WritePluginConsole("Trying to move players to target squads now!", "Work", 3);

                List<xPlayer>[] toMove = new List<xPlayer>[2];
                toMove[0] = new List<xPlayer>();
                toMove[1] = new List<xPlayer>();

                lock (teamChangesLock)
                {
                    foreach (xPlayer player in server)
                    {
                        WritePluginConsole(string.Format("^7{0,-15}^0 Current: ^6<{1} {2}>^0 Target: ^6<{3} {4}>", player.Name, player.TeamId, player.SquadId, player.TargetTeam, player.TargetSquad), "Info", 4);

                        if ((player.TeamId != player.TargetTeam || player.SquadId != player.TargetSquad) && player.TargetTeam == 1 && player.TeamId != 0)
                        {
                            toMove[0].Add(player);
                        }
                        else if ((player.TeamId != player.TargetTeam || player.SquadId != player.TargetSquad) && player.TargetTeam == 2 && player.TeamId != 0)
                        {
                            toMove[1].Add(player);
                        }
                    }
                }

                if (toMove[0].Count == 0 && toMove[1].Count == 0)
                {
                    this.ExecuteCommand("procon.protected.tasks.remove", "taskTryMoveTargetSquads");
                }

                // sort from highest to lowest dexterity to ensure pro become squad leader
                toMove[0].Sort(delegate(xPlayer player1, xPlayer player2)
                {
                    return player2.Dexterity.CompareTo(player1.Dexterity);
                });

                toMove[1].Sort(delegate(xPlayer player1, xPlayer player2)
                {
                    return player2.Dexterity.CompareTo(player1.Dexterity);
                });


                WritePluginConsole("==================================", "Info", 4);
                WritePluginConsole("Moving to Team 1: " + toMove[0].Count + " | Moving to Team 2: " + toMove[1].Count, "Info", 4);
                WritePluginConsole("==================================", "Info", 4);

                int k = 0;
                while (toMove[0].Count > 0 || toMove[1].Count > 0)
                {
                    if (toMove[k].Count > 0)
                    {
                        ProconMove(toMove[k][0].Name, toMove[k][0].TargetTeam, toMove[k][0].TargetSquad, true);
                        WritePluginConsole(string.Format("RoundBalance: ^7[{0,4}] {1,-15} ^6<{2} {3}> -> ^6<{4} {5}>", toMove[k][0].Tag, toMove[k][0].Name, toMove[k][0].TeamId, toMove[k][0].SquadId, toMove[k][0].TargetTeam, toMove[k][0].TargetSquad), "Info", 4);
                        toMove[k].RemoveAt(0);
                    }
                    k = (k + 1) % 2;
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in TryMoveTargetSquads", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        /// <summary>
        /// Move each player in the server to their assigned target team/squad.
        /// </summary>
        public void MoveToPlayersToTarget()
        {
            try
            {
                // HALF FINISHED

                WritePluginConsole("Move players to target squads now!", "Work", 3);

                int maxTeamSize = this.server.MaxPlayerCount / 2;

                List<xPlayer> toMove = new List<xPlayer>();
                List<xPlayer>[] team = new List<xPlayer>[2];
                team[0] = new List<xPlayer>(this.server.GetPlayers(1));
                team[1] = new List<xPlayer>(this.server.GetPlayers(2));

                // Both teams full?, kick last join
                if (team[0].Count == maxTeamSize && team[1].Count == maxTeamSize)
                {
                    this.server.Kick(KickCondition.LastJoin);

                    // reload teams
                    team[0] = new List<xPlayer>(this.server.GetPlayers(1));
                    team[1] = new List<xPlayer>(this.server.GetPlayers(2));
                }

                int startTeam = 0;
                int endTeam = 1;
                if (team[0].Count > team[1].Count)
                {
                    startTeam = 1;
                    endTeam = 0;
                }

                // remove players in correct position
                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < team[j].Count; i++)
                    {
                        if (team[j][i].TeamId == team[j][i].TargetTeam && team[j][i].SquadId == team[j][i].TargetSquad)
                        {
                            team[j].RemoveAt(i);
                            i--;
                        }
                    }
                }

                // sort dexterity (highest to lowest)
                team[0].Sort(delegate(xPlayer player1, xPlayer player2)
                {
                    return player2.Dexterity.CompareTo(player1.Dexterity);
                });

                team[1].Sort(delegate(xPlayer player1, xPlayer player2)
                {
                    return player2.Dexterity.CompareTo(player1.Dexterity);
                });

                List<xPlayer> squadLeaders = new List<xPlayer>();
                List<string> squads = new List<string>();

                // determine the squad leaders
                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < team[j].Count; i++)
                    {
                        if (team[j][i].TargetSquad != 0 && !squads.Contains(team[j][i].TargetTeam.ToString() + team[j][i].TargetSquad.ToString()))
                        {
                            squads.Add(team[j][i].TargetTeam.ToString() + team[j][i].TargetSquad.ToString());
                            squadLeaders.Add(team[j][i]);
                            team[j].RemoveAt(i);
                            i--;
                        }
                    }
                }

                // MOVE SQUAD LEADERS INTO POSITION
                squads = new List<string>();

                // starting team. squad change only.
                for (int i = 0; i < squadLeaders.Count; i++)
                {
                    if (squadLeaders[i].TeamId == startTeam && squadLeaders[i].TeamId == squadLeaders[i].TargetTeam)
                    {
                        toMove.Add(squadLeaders[i]);
                        squads.Add(squadLeaders[i].TargetTeam.ToString() + squadLeaders[i].TargetSquad.ToString());
                        squadLeaders.RemoveAt(i);
                        i--;
                    }
                }

                
                // move one player across to start team to free up a slot on end team.
                bool found = false;
                for (int i = 0; i < squadLeaders.Count; i++)
                {
                    if (squadLeaders[i].TeamId == endTeam + 1 && squadLeaders[i].TargetTeam == startTeam + 1)
                    {
                        toMove.Add(squadLeaders[i]);
                        squads.Add(squadLeaders[i].TargetTeam.ToString() + squadLeaders[i].TargetSquad.ToString());
                        squadLeaders.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    for (int i = 0; i < team[endTeam].Count; i++)
                    {
                        if (team[endTeam][i].TargetTeam == startTeam + 1 && squads.Contains(team[endTeam][i].TargetTeam.ToString() + team[endTeam][i].TargetSquad.ToString()))
                        {
                            toMove.Add(team[endTeam][i]);
                            team[endTeam].RemoveAt(i);
                            break;
                        }
                    }
                }

                // end team. squad change only.
                for (int i = 0; i < squadLeaders.Count; i++)
                {
                    if (squadLeaders[i].TeamId == endTeam && squadLeaders[i].TeamId == squadLeaders[i].TargetTeam)
                    {
                        toMove.Add(squadLeaders[i]);
                        squads.Add(squadLeaders[i].TargetTeam.ToString() + squadLeaders[i].TargetSquad.ToString());
                        squadLeaders.RemoveAt(i);
                        i--;
                    }
                }

                // alternate team switches
                int k = startTeam;
                int m = endTeam;
                while (squadLeaders.Count > 0)
                {
                    found = false;
                    for (int i = 0; i < squadLeaders.Count; i++)
                    {
                        if (squadLeaders[i].TeamId == k + 1)
                        {
                            toMove.Add(squadLeaders[i]);
                            squads.Add(squadLeaders[i].TargetTeam.ToString() + squadLeaders[i].TargetSquad.ToString());
                            squadLeaders.RemoveAt(i);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        for (int i = 0; i < team[k].Count; i++)
                        {
                            if (team[k][i].TargetTeam == m + 1 && squads.Contains(team[k][i].TargetTeam.ToString() + team[k][i].TargetSquad.ToString()))
                            {
                                toMove.Add(team[k][i]);
                                team[k].RemoveAt(i);
                                break;
                            }
                        }
                    }

                    k = (k + 1) % 2;
                    m = (m + 1) % 2;
                }


                // MOVE THE REST

                startTeam = k;
                endTeam = m;

                // starting team. squad change only.
                for (int i = 0; i < team[startTeam].Count; i++)
                {
                    if (team[startTeam][i].TeamId == team[startTeam][i].TargetTeam)
                    {
                        toMove.Add(team[startTeam][i]);
                        team[startTeam].RemoveAt(i);
                        i--;
                    }
                }

                // move one player across to start team to free up a slot on end team.
                for (int i = 0; i < team[endTeam].Count; i++)
                {
                    if (team[endTeam][i].TargetTeam == startTeam + 1)
                    {
                        toMove.Add(team[endTeam][i]);
                        team[endTeam].RemoveAt(i);
                        break;
                    }
                }

                // end team. squad change only.
                for (int i = 0; i < team[endTeam].Count; i++)
                {
                    if (team[endTeam][i].TeamId == team[endTeam][i].TargetTeam)
                    {
                        toMove.Add(team[endTeam][i]);
                        team[endTeam].RemoveAt(i);
                        i--;
                    }
                }

                // alternate team switches
                k = startTeam;
                while (team[0].Count > 0 || team[1].Count > 0)
                {
                    if (team[k].Count > 0)
                    {
                        toMove.Add(team[k][0]);
                        team[k].RemoveAt(0);
                    }

                    k = (k + 1) % 2;
                }

                // execute moves
                for (int i = 0; i < toMove.Count; i++)
                {
                    WritePluginConsole(string.Format("RoundBalance: ^7[{0,4}] {1,-15} ^6<{2} {3}> -> ^6<{4} {5}>", toMove[i].Tag, toMove[i].Name, toMove[i].TeamId, toMove[i].SquadId, toMove[i].TargetTeam, toMove[i].TargetSquad), "Info", 4);
                    ProconMove(toMove[i].Name, toMove[i].TargetTeam, toMove[i].TargetSquad, true);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in TryMoveTargetSquads", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        /// <summary>
        /// If either team is full it will move a player to the other team so there is space to move.
        /// </summary>
        public void FreeUpSlots()
        {
            try
            {
                if (this.server.TeamCount(1) == this.server.MaxPlayerCount / 2 || this.server.TeamCount(2) == this.server.MaxPlayerCount / 2)
                {
                    int moveFrom = 1;
                    int moveTo = 2;
                    if (server.TeamCount(1) < server.TeamCount(2))
                    {
                        moveFrom = 2;
                        moveTo = 1;
                    }

                    List<xPlayer> loneWolves = new List<xPlayer>(this.server.GetPlayers(moveFrom, 0));
                    if (loneWolves.Count > 0)
                    {
                        WritePluginConsole("Moving ^7" + loneWolves[0].Name + "^0 to other team to ensure there is space to move players.", "Work", 3);
                        ProconMove(loneWolves[0].Name, moveTo, 0, true);
                    }
                    else if (roundKeepSquadsTogether == enumBoolYesNo.No)
                    {
                        List<xPlayer> players = new List<xPlayer>(this.server.GetPlayers(moveFrom));
                        if (players.Count > 0)
                        {
                            WritePluginConsole("Moving ^7" + players[0].Name + "^0 to other team to ensure there is space to move players.", "Work", 3);
                            ProconMove(players[0].Name, moveTo, 0, true);
                        }
                    }
                }
                else if (this.server.PlayerCount == this.server.MaxPlayerCount)
                {
                    WritePluginConsole("Both teams are full, there is no room to move players. Not sure what to do...", "Warning", 2);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in FreeUpSlots", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        /// <summary>
        /// Groups players together into clusters of friends. That being anyone who is connected to another player in the server through the battlelog friends list.
        /// </summary>
        /// <param name="tree">List of players to clusterize.</param>
        /// <returns>A list of friend clusters</returns>
        public List<List<xPlayer>> ClusterizeFriendTree(List<xPlayer> tree)
        {
            try
            {
                List<xPlayer> allPlayers = new List<xPlayer>(tree);
                List<List<xPlayer>> clusters = new List<List<xPlayer>>();

                int clustersfound = 0;
                while (tree.Count != 0)
                {
                    List<xPlayer> cluster = FindConnections(allPlayers, tree[0].Name, new List<xPlayer>());

                    foreach (xPlayer player in cluster)
                    {
                        for (int i = 0; i < tree.Count; i++)
                        {
                            if (tree[i].Name == player.Name)
                            {
                                tree.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    clusters.Add(cluster);
                    clustersfound++;
                }

                return clusters;
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in ClusterizeFriendTree", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
                return null;
            }
        }

        /// <summary>
        /// Fractures a large friend cluster into two smaller clusters.
        /// </summary>
        /// <param name="bigCluster">Large cluster of friends</param>
        /// <returns>An array of two smaller friend clusters.</returns>
        public List<xPlayer>[] FractureCluster(List<xPlayer> bigCluster)
        {
            List<xPlayer>[] smallerClusters = new List<xPlayer>[2];
            smallerClusters[0] = new List<xPlayer>();
            smallerClusters[1] = new List<xPlayer>();

            List<KeyValuePair<int, int>> connectivity = new List<KeyValuePair<int, int>>();

            for (int i = 0; i < bigCluster.Count; i++)
            {
                List<xPlayer> friends = GetFriends(bigCluster, bigCluster[i]);
                connectivity.Add(new KeyValuePair<int, int>(i, friends.Count));
            }

            // sort from least to most friends
            connectivity.Sort(delegate(KeyValuePair<int, int> test1, KeyValuePair<int, int> test2)
            {
                return test1.Value.CompareTo(test2.Value);
            });



            StreamWriter sw = new StreamWriter(String.Format("./Logs/{0}_{1}/{2:yyyyMMdd}_PlayerInformation.log", m_strHostName, m_strPort, DateTime.Now), true);
            sw.WriteLine("");
            sw.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            sw.WriteLine("Fracturing Big Friend Cluster:");
            foreach (KeyValuePair<int, int> pair in connectivity)
            {
                string line = bigCluster[pair.Key].Name + " " + pair.Value;
                sw.WriteLine(line);
            }
            sw.WriteLine("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            sw.WriteLine("");
            sw.Close();

            // if there are some players with less friends
            if (connectivity[connectivity.Count - 1].Value - connectivity[0].Value >= 2)
            {
                // seperate off leacher and his friends
                smallerClusters[0].Add(bigCluster[connectivity[0].Key]);
                List<xPlayer> newFriends = GetFriends(bigCluster, bigCluster[connectivity[0].Key]);
                for (int i = 0; i < newFriends.Count; i++)
                {
                    smallerClusters[0].Add(newFriends[i]);
                }

                // add rest to other cluster
                for (int i = 0; i < bigCluster.Count; i++)
                {
                    if (!PlayerInList(smallerClusters[0], bigCluster[i]))
                    {
                        smallerClusters[1].Add(bigCluster[i]);
                    }
                }
            }
            //they all know each other, split down the middle
            else
            {
                for (int i = 0; i < bigCluster.Count; i++)
                {
                    smallerClusters[i % 2].Add(bigCluster[i]);
                }
            }

            return smallerClusters;
        }

        /// <summary>
        /// Finds friend connections between a player and the other players on the server.
        /// </summary>
        /// <param name="allPlayers">List of all players in the server.</param>
        /// <param name="startPlayerName">Player to start the friend connection search with.</param>
        /// <param name="foundPlayers">Players in 'allPlayers' to exclude from adding to the connection web. Used in the recursive function, generally it should be set to an empty list.</param>
        /// <returns>A list of players (including the starting player) that are connected by players in allplayers and in their friends list.</returns>
        public List<xPlayer> FindConnections(List<xPlayer> allPlayers, string startPlayerName, List<xPlayer> foundPlayers)
        {
            try
            {
                if (PlayerFromName(foundPlayers, startPlayerName) == null)
                {
                    xPlayer foundPlayer = PlayerFromName(allPlayers, startPlayerName);
                    foundPlayers.Add(foundPlayer);
                }


                xPlayer startPlayer = PlayerFromName(allPlayers, startPlayerName);
                if (startPlayer != null)
                {
                    string[] friends = startPlayer.Friends;
                    for (int i = 0; i < friends.Length; i++)
                    {
                        bool found = false;
                        foreach (xPlayer player in foundPlayers)
                        {
                            if (player.Name == friends[i])
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            xPlayer playerToAdd = PlayerFromName(allPlayers, friends[i]);
                            if (playerToAdd != null)
                            {
                                foundPlayers.Add(playerToAdd);
                                foundPlayers = new List<xPlayer>(FindConnections(allPlayers, friends[i], foundPlayers));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in FindConnections", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
            return foundPlayers;
        }

        //public List<xPlayer> GetMutualFriends(List<xPlayer>[] allPlayers, xPlayer playerA, xPlayer playerB)
        //{
        //    List<xPlayer> mutualFriends = new List<xPlayer>();

        //    int Apos = FindPosInArray(allPlayers, playerA);
        //    int Bpos = FindPosInArray(allPlayers, playerB);

        //    for (int i = 1; i < allPlayers[Apos].Count; i++)
        //    {
        //        for (int j = 1; j < allPlayers[Bpos].Count; j++)
        //        {
        //            if (allPlayers[Apos][i].Name == allPlayers[Bpos][j].Name)
        //            {
        //                mutualFriends.Add(allPlayers[Apos][i]);
        //                break;
        //            }
        //        }
        //    }

        //    return mutualFriends;
        //}

        #endregion

        #region Other

        public void SkillMonitor()
        {
            try
            {
                WritePluginConsole("Starting Skill Monitor", "Info", 5);

                int teamAscore = this.server.TeamScores[0].Score;
                int teamBscore = this.server.TeamScores[1].Score;

                int totalRank0 = 0;
                int totalSpm0 = 0;
                int totalSkill0 = 0;
                int totalPlayers0 = 0;
                int totalRank1 = 0;
                int totalSpm1 = 0;
                int totalSkill1 = 0;
                int totalPlayers1 = 0;
                StreamWriter sw = new StreamWriter("SkillMonitor.txt", true);
                for (int i = 0; i < this.server.PlayerCount; i++)
                {
                    if (this.server[i].TeamId == 1)
                    {
                        totalRank0 += this.server[i].Rank;
                        totalSpm0 += this.server[i].SPM;
                        totalSkill0 += this.server[i].Skill;
                        totalPlayers0++;
                        //    sw.WriteLine("TEAM0: Name: " + string.Format("{0,-20}", this.server[i].Name) + "Rank: " + string.Format("{0,3}", this.server[i].Rank) + "\tSPM: " + string.Format("{0,4}", this.server[i].SPM) + "\tSkill: " + string.Format("{0,5}", this.server[i].Skill));
                    }
                    else if (this.server[i].TeamId == 2)
                    {
                        totalRank1 += this.server[i].Rank;
                        totalSpm1 += this.server[i].SPM;
                        totalSkill1 += this.server[i].Skill;
                        totalPlayers1++;
                        //     sw.WriteLine("TEAM1: Name: " + string.Format("{0,-20}", this.server[i].Name) + "Rank: " + string.Format("{0,3}", this.server[i].Rank) + "\tSPM: " + string.Format("{0,4}", this.server[i].SPM) + "\tSkill: " + string.Format("{0,5}", this.server[i].Skill));
                    }
                }

                //WritePluginConsole("   Skill: " + string.Format("{0,6}", totalSkill0) + " vs " + string.Format("{0,-6}", totalSkill1) + "       Players: " + string.Format("{0,3}", totalPlayers0) + " vs " + string.Format("{0,-3}", totalPlayers1), "Info", 3);

                if (totalPlayers0 != 0 && totalPlayers1 != 0)
                {
                    int scoreDiff = teamAscore - teamBscore;

                    double aveRank = (double)(totalRank0 + totalRank1) / (totalPlayers0 + totalPlayers1);
                    double aveRank0 = (double)totalRank0 / totalPlayers0;
                    double aveRank1 = (double)totalRank1 / totalPlayers1;
                    double rankPercDiff = (aveRank0 - aveRank1) / aveRank;

                    double aveSpm = (double)(totalSpm0 + totalSpm1) / (totalPlayers0 + totalPlayers1);
                    double aveSpm0 = (double)totalSpm0 / totalPlayers0;
                    double aveSpm1 = (double)totalSpm1 / totalPlayers1;
                    double spmPercDiff = (aveSpm0 - aveSpm1) / aveSpm;

                    int spmDiff = totalSpm0 - totalSpm1;
                    int skillDiff = totalSkill0 - totalSkill1;
                    int playerDiff = totalPlayers0 - totalPlayers1;
                    double aveSkill = (double)(totalSkill0 + totalSkill1) / (totalPlayers0 + totalPlayers1);
                    double aveSkill0 = (double)totalSkill0 / totalPlayers0;
                    double aveSkill1 = (double)totalSkill1 / totalPlayers1;
                    double skillPercDiff = (aveSkill0 - aveSkill1) / aveSkill;

                    sw.WriteLine(string.Format("{0,5}", scoreDiff) + "\t" + string.Format("{0,5}", spmDiff) + "\t" + string.Format("{0,5}", skillDiff) + "\t" + string.Format("{0,5}", playerDiff));

                    sw.Close();
                }
                WritePluginConsole("Skill Monitor Done!", "Info", 5);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in SkillMonitor", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

        }

        public void ShowInformation()
        {
            try
            {
                StreamWriter sw = new StreamWriter(String.Format("./Logs/{0}_{1}/{2:yyyyMMdd}_PlayerInformation.log", m_strHostName, m_strPort, DateTime.Now), true);
                for (int i = 0; i < 3; i++)
                {
                    int totalDexterity = 0;

                    sw.WriteLine("===============");
                    sw.WriteLine("TEAM " + i + ":   ");//+ //teamScores[i - 1].Score);
                    sw.WriteLine("===============");
                    for (int j = 0; j < 8; j++)
                    {
                        List<xPlayer> playerList = new List<xPlayer>(server.GetPlayers(i, j));
                        foreach (xPlayer player in playerList)
                        {
                            string fri = "";
                            foreach (string friend in player.Friends)
                            {
                                if (server.Contains(friend))
                                {
                                    fri += friend + " | ";
                                }
                            }
                            sw.WriteLine(string.Format("[{0,-4}]", player.Tag) + "     " + string.Format("{0,-20}", player.Name) + "     " + string.Format("{0,3}", player.Rank.ToString()) + "     " + string.Format("{0,4}", player.SPM.ToString()) + "     " + string.Format("{0,5}", player.Alive.ToString()) + "     " + string.Format("{0,3}", player.Moves.ToString()) + "     " + player.SquadId.ToString() + "     " + fri);
                            totalDexterity += player.Dexterity;
                        }
                    }

                    sw.WriteLine("______________________________________________________________________________________________________");
                    sw.WriteLine("Total Dexterity: " + totalDexterity.ToString());
                    sw.WriteLine("______________________________________________________________________________________________________");
                    sw.WriteLine("");
                }

                sw.WriteLine("######################################################################################################");
                sw.WriteLine("");
                sw.Close();
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in ShowInformation", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        //private string GetMapListString()
        //{
        //    List<string> maps = new List<string>(GetMapList("{PublicLevelName}"));
        //    string maplist = "";

        //    for (int i = 0; i < maps.Count; i++)
        //    {
        //        if (!maplist.Contains(maps[i]))
        //        {
        //            maplist += maps[i] + "|";
        //        }
        //    }
        //    maplist = maplist.Substring(0, maplist.Length - 1);
        //    return maplist;
        //}

        //private string GetMapListString(string map)
        //{
        //    List<string> modes = new List<string>(GetMapList("{GameMode}", new string[] { map }));
        //    //List<string> maplist = new List<string>();
        //    string modelist = "";

        //    for (int i = 0; i < modes.Count; i++)
        //    {
        //        if (!modelist.Contains(modes[i]))
        //        {
        //            modelist += modes[i] + "|";
        //        }
        //    }
        //    modelist = modelist.Substring(0, modelist.Length - 1);
        //    return modelist;
        //}

        private void ProcessChatMessage(string speaker, string message)
        {

        }

        private void ProcessConsoleCommand(string command)
        {
            try
            {
                if (command == "print playerlist")
                {

                }

            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in ProcessConsoleCommand", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        private string GenerateGraphURL(int round)
        {
            string url = "";
            try
            {
                if (this.roundMap.Count > round)
                {
                    string scoreValues = "";
                    foreach (int score in this.roundScores[round])
                    {
                        scoreValues += score / 10 + ",";
                    }
                    scoreValues = scoreValues.Substring(0, scoreValues.Length - 1);

                    string countValues = "";
                    foreach (int count in this.roundPlayerCount[round])
                    {
                        countValues += count * 10 + ",";
                    }
                    countValues = countValues.Substring(0, countValues.Length - 1);

                    string dexValues = "";
                    foreach (int dex in this.roundDexterity[round])
                    {
                        dexValues += dex / 5000 + ",";
                    }
                    dexValues = dexValues.Substring(0, dexValues.Length - 1);

                    string mapName = this.roundMap[round].Replace(' ', '+');

                    url = string.Format("http://chart.apis.google.com/chart?chxl=0:|-50%25|0%25|%2B50%25&chxr=0,-5,100&chxt=r&chs=780x300&cht=lc&chco=DA3B15,F7A10A,4582E7&chd=t:{0}|{1)|{2}&chdl=Score|Dexterity|Player+Count&chls=1|1|1&chtt={3}+%5BCQ%5D", scoreValues, countValues, dexValues, mapName);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in GenerateGraphURL", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

            return url;
        }

        #region SettingsImporter

        /// <summary>
        /// Read the other servers names from their config files.
        /// </summary>
        /// <returns>enum string listing the other servernames.</returns>
        private string GetOtherServersEnum()
        {
            string otherServersEnum = "enum.ImportSettings(Select Server...";
            try
            {
                string[] filePaths = Directory.GetFiles(@".\Configs\", "*.cfg");
                otherServerNames = new Dictionary<string, string>();
                foreach (string filePath in filePaths)
                {
                    if (filePath.Contains("_"))
                    {
                        StreamReader sr = new StreamReader(filePath);
                        string line = "";
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.Contains("\"xEquilibrium\" \"serverName\""))
                            {
                                string otherServerName = line.Substring(66, line.Length - 67);                            
                                if (otherServerName.CompareTo(this.serverName) != 0)
                                {
                                    otherServerName = otherServerName.Replace("|", "");
                                    otherServersEnum += "|" + otherServerName;
                                    otherServerNames.Add(otherServerName, filePath);
                                }
                                break;
                            }
                        }
                        sr.Close();
                    }
                }              
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in GetOtherServersEnum", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
            otherServersEnum += ")";
            return otherServersEnum;
        }

        /// <summary>
        /// Apply the same settings from another server
        /// </summary>
        /// <param name="otherServerName">Shortened name of the other server</param>
        private void SetOtherServerSettings(string otherServerName)
        {
            try
            {
                string serverEnum = GetOtherServersEnum();

                if (otherServerNames.ContainsKey(otherServerName))
                {
                    StreamReader sr = new StreamReader(otherServerNames[otherServerName]);

                    bool success = false;
                    string line = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("\"xEquilibrium\"") && !line.Contains("serverName"))
                        {
                            Match regexMatch = Regex.Match(line, @".+?""xEquilibrium"" ""(.+?)"" ""(.+?)""");
                            if (regexMatch.Success)
                            {
                                SetPluginVariable(regexMatch.Groups[1].ToString(), regexMatch.Groups[2].ToString());
                                success = true;
                            }
                        }
                    }
                    sr.Close();
                    if (success)
                    {
                        MessageBox.Show("Settings Import Successful!", "Import Settings", MessageBoxButtons.OK, MessageBoxIcon.None);
                    }
                    else
                    {
                        MessageBox.Show("Settings Import Failed!", "Import Settings", MessageBoxButtons.OK, MessageBoxIcon.None);
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception Caught in SetOtherServerSettings", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        #endregion

        #endregion

        #region Tools

        /// <summary>
        /// Writes a message in the procon plugin output window.
        /// </summary>
        /// <param name="message">Message to write</param>
        /// <param name="tag">A tag describing the category of the message. The preset values are: "Info", "Work", "Warning", or "Error" </param>
        /// <param name="level">Level of importance from 1 being extremely important to 5 being unimportant.</param>
        public void WritePluginConsole(string message, string tag, int level)
        {
            if (tag == "Error")
            {
                tag = "^8" + tag;
            }
            else if (tag == "Warning")
            {
                tag = "^3" + tag;
            }
            else if (tag == "Work")
            {
                tag = "^4" + tag;
            }
            else
            {
                tag = "^5" + tag;
            }
            string line = "^b[" + this.GetPluginName() + "] " + tag + ": ^0^n" + message;

            if (this.globalDebugLevel >= level)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", line);
            }
        }

        /// <summary>
        /// Centers a string with padded characters.
        /// </summary>
        /// <param name="s">String to center.</param>
        /// <param name="width">Width of padded string.</param>
        /// <param name="c">Character to pad with.</param>
        /// <returns>A string centered with padded characters.</returns>
        public string PadCenter(string s, int width, char c)
        {
            if (s == null || width <= s.Length) return s;

            int padding = width - s.Length;
            return s.PadLeft(s.Length + padding / 2, c).PadRight(width, c);
        }

        /// <summary>
        /// Gets the friends of a players that are in the server.
        /// </summary>
        /// <param name="allPlayers">List of all players in the server.</param>
        /// <param name="player">Player to get the friends of.</param>
        /// <returns>List of friends of 'player' in 'allplayers'</returns>
        public List<xPlayer> GetFriends(List<xPlayer> allPlayers, xPlayer player)
        {
            List<xPlayer> friends = new List<xPlayer>();

            for (int i = 0; i < allPlayers.Count; i++)
            {
                if (StringInArray(allPlayers[i].Friends, player.Name))
                {
                    friends.Add(allPlayers[i]);
                }
            }
            return friends;
        }

        /// <summary>
        /// Determines if a player exists within a list of players.
        /// </summary>
        /// <param name="playerList">List of players to check from.</param>
        /// <param name="player">Player to check for.</param>
        /// <returns>true, if the player is within the list, otherwise, false.</returns>
        public bool PlayerInList(List<xPlayer> playerList, xPlayer player)
        {
            bool found = false;
            foreach (xPlayer testPlayer in playerList)
            {
                if (testPlayer.Name == player.Name)
                {
                    found = true;
                    break;
                }
            }
            return found;
        }

        /// <summary>
        /// Finds a player within a player list from the players name.
        /// </summary>
        /// <param name="playerList">Playerlist to search from the playername in.</param>
        /// <param name="playername">Playername to search from</param>
        /// <returns>If the player is found then the player from the list will be return, otherwise, null</returns>
        public xPlayer PlayerFromName(List<xPlayer> playerList, string playername)
        {
            foreach (xPlayer player in playerList)
            {
                if (playername == player.Name)
                {
                    return player;
                }
            }
            return null;
        }

        public bool StringInArray(string[] array, string text)
        {
            foreach (string line in array)
            {
                if (line == text)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Totals the dexterity of the players from the playerlist
        /// </summary>
        /// <param name="playerList">List of players to total.</param>
        /// <returns>Total dexterity of all players in playerlist</returns>
        public int SumDexterityofPlayers(List<xPlayer> playerList)
        {
            int sum = 0;
            foreach (xPlayer player in playerList)
            {
                sum += player.Dexterity;
            }
            return sum;
        }

        #endregion

        #region Classes

        /// <summary>
        /// A collection of players on a server.
        /// </summary>
        public class xServer : IEnumerable
        {
            private xEquilibrium plugin = null;


            private List<xPlayer> playerList = new List<xPlayer>();
            private Dictionary<string, int> joinMoveQueue = new Dictionary<string, int>();

            int[] teamCount = new int[5];
            int[,] squadCount = new int[5,9];

            private List<MaplistEntry> currMapList = new List<MaplistEntry>();
            private List<TeamScore> teamScores = null;
            private int maxPlayers = 0;
            private string currentMap = "";
            private string currentMode = "";
            private string nextMap = "";
            private string nextMode = "";
            private double winningDexCompSetting = 1.0;
            private int disableTicketThreshold = 15;
            private double mapBiasSetting = 1.0;
            private int highestTicketCount = -1;
            private int gamemodeCount = 100;
            private bool midgame = true;

            int workerLimit = 5;                                                    /// <summary>Maximum number of stats fetching worker threads to be running at one time.</summary>
            List<Thread> workerthreads = new List<Thread>();
            List<string> statsNeeded = new List<string>();

            public xServer(List<CPlayerInfo> currentPlayers, xEquilibrium plugin)
            {
                this.plugin = plugin;
                foreach (CPlayerInfo cpi in currentPlayers)
                {
                    this.AddPlayer(cpi.SoldierName, cpi.TeamID, cpi.SquadID);
                }
            }

            //public xServer(List<xPlayer> players)
            //{
            //    foreach(xPlayer player in players)
            //    {
            //        playerList.Add(player);
            //    }
            //}

            //public xServer(xPlayer player)
            //{
            //    this.playerList.Add(player);
            //}

            public void AddPlayer(string name)
            {
                this.RemovePlayer(name);
                playerList.Add(new xPlayer(name));
                this.UpdateStats(name);
            }

            public void AddPlayer(string name, int teamId, int squadId)
            {
                this.RemovePlayer(name);
                xPlayer newPlayer = new xPlayer(name);
                this.UpdateStats(name);
                newPlayer.TeamId = teamId;
                newPlayer.SquadId = squadId;
                newPlayer.TargetTeam = teamId;
                newPlayer.TargetSquad = squadId;
                playerList.Add(newPlayer);             
            }

            public void AddPlayer(xPlayer player)
            {
                this.playerList.Add(player);
            }

            public void RemovePlayer(string name)
            {
                for (int i = 0; i < this.playerList.Count; i++)
                {
                    if (playerList[i].Name == name)
                    {
                        this.playerList.RemoveAt(i);
                        i--;
                    }
                }
                for (int i = 0; i < this.statsNeeded.Count; i++)
                {
                    if (statsNeeded[i] == name)
                    {
                        this.statsNeeded.RemoveAt(i);
                        i--;
                    }
                }
            }

            public bool Contains(string name)
            {
                foreach (xPlayer player in playerList)
                {
                    if (player.Name == name)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Adds a player to the queue of players requiring a stats update.
            /// If the number of worker threads is less than the limit then a new worker thread is started.
            /// </summary>
            /// <param name="name">Player name to update stats of.</param>
            private void UpdateStats(string name)
            {
                statsNeeded.Add(name);

                this.CheckWorkerStates();
                if (workerthreads.Count < workerLimit)
                {
                    Thread thread = new Thread(new ThreadStart(StatsWorker));
                    thread.Name = "StatsFetcher" + workerthreads.Count;
                    thread.Start();
                    workerthreads.Add(thread);
                }
            }

            /// <summary>
            /// Fetches stats of each player in the stats queue until empty.
            /// </summary>
            private void StatsWorker()
            {
                try
                {
                    while (statsNeeded.Count != 0)
                    {
                        string playername = "";
                        while (playername == "")
                        {
                            try
                            {
                                lock (statsNeeded)
                                {
                                    playername = statsNeeded[0];                                    
                                    Console.WriteLine("Stats to get: " + statsNeeded.Count);
                                    if (this[playername] != null)
                                    {
                                        statsNeeded.RemoveAt(0);
                                    }
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Statsneeded grab failed");
                                Thread.Sleep(50);
                            }
                        }
                        this[playername].FetchStats();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception caught in StatsWorker");
                    Console.WriteLine(e.Message);
                }
            }

            /// <summary>
            /// Updates the list of active worker threads
            /// </summary>
            private void CheckWorkerStates()
            {
                for (int i = 0; i < this.workerthreads.Count; i++)
                {
                    if (workerthreads[i].ThreadState != ThreadState.Running)
                    {
                        this.workerthreads.RemoveAt(i);
                        i--;
                    }
                }
            }

            public List<xPlayer> GetPlayers(int teamId, int squadId)
            {
                List<xPlayer> gotPlayers = new List<xPlayer>();
                foreach (xPlayer player in playerList)
                {
                    if (player.TeamId == teamId && player.SquadId == squadId)
                    {
                        gotPlayers.Add(player);
                    }
                }
                return gotPlayers;
            }

            public List<xPlayer> GetPlayers(int teamId)
            {
                List<xPlayer> gotPlayers = new List<xPlayer>();
                foreach (xPlayer player in playerList)
                {
                    if (player.TeamId == teamId)
                    {
                        gotPlayers.Add(player);
                    }
                }
                return gotPlayers;
            }

            public List<xPlayer> GetPlayers()
            {
                return playerList;
            }

            public void SwitchTargetTeams()
            {
                this.highestTicketCount = -1; // reset for new round
                foreach (xPlayer player in playerList)
                {
                    if (player.TargetTeam == 1)
                    {
                        player.TargetTeam = 2;
                    }
                    else if (player.TargetTeam == 2)
                    {
                        player.TargetTeam = 1;
                    }
                }
            }

            public void SetEverybodyAlive(bool value)
            {
                foreach (xPlayer player in playerList)
                {
                    player.Alive = value;
                }
            }

            public int GetEmptiestSquadId(int team)
            {
                RefreshTeamCounts();
                int min = 4;
                int squad = 0;
                for (int i = 1; i < 9; i++)
                {
                    if (this.squadCount[team, i] < min)
                    {
                        min = this.squadCount[team, i];
                        squad = i;
                    }
                }
                return min;
            }

            public int GetFullestSquadId(int team)
            {
                RefreshTeamCounts();
                int max = 0;
                int squad = 0;
                for (int i = 1; i < 9; i++)
                {
                    if (this.squadCount[team, i] > max && this.squadCount[team, i] < 4)
                    {
                        max = this.squadCount[team, i];
                        squad = i;
                    }
                }
                return squad;
            }

            private void RefreshTeamCounts()
            {
                this.teamCount = new int[3];
                this.squadCount = new int[3, 65];
                foreach (xPlayer player in playerList)
                {
                    this.teamCount[player.TeamId]++;
                    this.squadCount[player.TeamId, player.SquadId]++;
                }
            }

            public int PlayerCount
            {
                get { return playerList.Count; }
            }

            public int NoStatsCount
            {
                get
                {
                    int i = 0;
                    foreach (xPlayer player in playerList)
                    {
                        if (!player.GotStats)
                        {
                            i++;
                        }
                    }
                    return i;
                }
            }

            public int TeamCount(int teamId)
            {
                RefreshTeamCounts();
                return this.teamCount[teamId];
            }

            public int SquadCount(int teamId, int squadId)
            {
                RefreshTeamCounts();
                return this.squadCount[teamId, squadId];
            }

            public int TeamDexterity(int teamId)
            {
                int totalDexterity = 0;
                foreach (xPlayer player in playerList)
                {
                    if (player.TeamId == teamId)
                    {
                        totalDexterity += player.Dexterity;
                    }
                }
                return totalDexterity;             
            }

            public int TeamAdjustedDexterity(int teamId)
            {
                int scoreBiasBonus = 0;
                
                if (this.midgame && currentMode.Contains("Conquest"))
                {
                    scoreBiasBonus = (int)(Math.Abs(this.teamScores[0].Score - this.teamScores[1].Score) * this.WinningTeamBiasSetting * 5.0 * (double)this.PlayerCount / (double)this.highestTicketCount);
                }
                else if (this.midgame && currentMode.Contains("DeathMatch"))
                {

                }

                Console.WriteLine(this.teamScores[0].Score + " " + this.teamScores[1].Score + " " + this.WinningTeamBiasSetting + " " + this.PlayerCount + " " + this.highestTicketCount + "=" + scoreBiasBonus);
                int adjustedDexterity = this.TeamDexterity(teamId);

                if ((this.currentMap.CompareTo("MP_Subway") == 0 || this.currentMap.CompareTo("XP1_001") == 0 || this.currentMap.CompareTo("XP1_003") == 0 || this.currentMap.CompareTo("XP1_004") == 0) && currentMode.Contains("Conquest") && teamId == 1)
                {
                    adjustedDexterity = (int)(adjustedDexterity * 1.1);
                }

                if ((this.teamScores[0].Score > this.teamScores[1].Score && teamId == 1) || (this.teamScores[0].Score < this.teamScores[1].Score && teamId == 2))
                {
                    adjustedDexterity += scoreBiasBonus;
                }



                return adjustedDexterity;
            }

            public string CurrentMap
            {
                get { return this.currentMap; }
                set { this.currentMap = value; }
            }

            public string CurrentMode
            {
                get { return this.currentMode; }
                set { this.currentMode = value; }
            }

            public string NextMap
            {
                get { return this.nextMap; }
                set { this.nextMap = value; }
            }

            public string NextMode
            {
                get { return this.nextMode; }
                set { this.nextMode = value; }
            }

            public bool GamemodeSupported
            {
                get {
                        if (this.CurrentMode.Contains("Squad"))
                        {
                            return false;
                        }
                        return true;
                    }
            }

            public int MaxPlayerCount
            {
                get { return this.maxPlayers; }
                set { this.maxPlayers = value; }
            }

            public double WinningTeamBiasSetting
            {
                get { return this.winningDexCompSetting; }
                set { this.winningDexCompSetting = value; }
            }

            public int DisableTicketThreshold
            {
                set { this.disableTicketThreshold = value; }
            }

            public int GameModeCount
            {
                get { return this.gamemodeCount; }
                set { this.gamemodeCount = value; }
            }

            public bool Midgame
            {
                get { return this.midgame; }
                set { this.midgame = value; }
            }

            public bool EnableLiveBalance
            {
                get
                {
                    if (this.currentMode.Contains("Conquest"))
                    {
                        int threshold = (int)((double)this.highestTicketCount * this.disableTicketThreshold / 100.0);
                        Console.WriteLine("CQ threshold: " + threshold);
                        foreach (TeamScore ts in this.teamScores)
                        {
                            if (ts.Score < threshold)
                            {
                                return false;
                            }
                        }
                    }
                    else if (this.currentMode.Contains("DeathMatch"))
                    {
                        int threshold = (int)(100.0 * this.gamemodeCount / 100.0 * (100.0 - this.disableTicketThreshold) / 100.0);
                        Console.WriteLine("DM threshold: " + threshold);
                        foreach (TeamScore ts in this.teamScores)
                        {
                            if (ts.Score > threshold)
                            {
                                return false;
                            }
                        }
                    }
                    else if (this.currentMode.Contains("SquadDeathmatch"))
                    {
                        int threshold = (int)(50.0 * this.gamemodeCount / 100.0 * this.disableTicketThreshold / 100.0);
                        foreach (TeamScore ts in this.teamScores)
                        {
                            if (ts.Score > threshold)
                            {
                                return false;
                            }
                        }
                    }
                    else if (this.currentMode.Contains("Rush"))
                    {
                        
                    }
                    return true;
                }
            }

            public double MapBias
            {
                get { return this.mapBiasSetting; }
                set { this.mapBiasSetting = value; }
            }

            public void Kick(KickCondition condition)
            {
                string playerName = "";
                if (condition == KickCondition.LastJoin)
                {
                    DateTime latestTime = DateTime.MinValue;

                    foreach (xPlayer player in this.playerList)
                    {
                        if (player.TeamId != 0 && player.TimeJoined > latestTime && !player.Whitelisted)
                        {
                            latestTime = player.TimeJoined;
                            playerName = player.Name;
                        }
                    }
                }

                if (playerName != "")
                {
                    this.plugin.WritePluginConsole("^7" + playerName + "^0 was kicked to open a slot so the Round Balancer can run.", "Work", 3);
                    this.plugin.ExecuteCommand("procon.protected.send", "admin.kickPlayer", playerName, "Sorry, need a slot to shuffle teams and you were last to join.");
                    this.RemovePlayer(playerName);
                }
            }

            public List<MaplistEntry> CurrentMaplist
            {
                get { return this.currMapList; }
                set { this.currMapList = value; }
            }

            public List<TeamScore> TeamScores
            {
                get { return this.teamScores; }
                set
                {
                    this.teamScores = value;
                    if (this.highestTicketCount == -1 && this.teamScores.Count > 0)
                    {
                        this.highestTicketCount = this.teamScores[0].Score;
                        for (int i = 1; i < this.teamScores.Count; i++)
                        {
                            if (this.teamScores[i].Score < this.highestTicketCount)
                            {
                                highestTicketCount = this.teamScores[i].Score;
                            }
                        }
                    }
                }
            }

            public xPlayer this[int i]
            {
                get
                {
                    if (i >= 0 && i < this.playerList.Count)
                    {
                        return playerList[i];
                    }
                    else
                    {
                        throw new Exception("Index out of valid range");
                    }
                }
                set
                {
                    if (i >= 0 && i < this.playerList.Count)
                    {
                        playerList[i] = value;
                    }
                }
            }

            public xPlayer this[string name]
            {
                get
                {
                    foreach (xPlayer player in this.playerList)
                    {
                        if (player.Name == name)
                        {
                            return player;
                        }
                    }

                    return null;       
                }
                set
                {
                    bool success = false;
                    for (int i = 0; i < this.playerList.Count; i++)
                    {
                        if (this.playerList[i].Name == name)
                        {
                            this.playerList[i] = value;
                            success = true;
                            break;
                        }
                    }
                    if (!success)
                    {
                        throw new Exception("Player not set");
                    }
                }
            }

            // IEnumerable<xPlayer> Members
            public IEnumerator GetEnumerator()
            {
                return (playerList as IEnumerable).GetEnumerator();
            }
        }

        public class xPlayer : IComparable<xPlayer>
        {
            private bool gotStats;
            private int moves;
            private bool alive;
            private bool whitelisted;

            private int teamId;
            private int squadId;
            private int targetTeam;
            private int targetSquad;
            private int rank;
            private int spm;
            private int skill;

            private string name;
            private string tag = "";
            private string[] friends = new string[0];

            private string statsErrorMessage = "";

            private DateTime expires;
            private DateTime timeJoined;

            public xPlayer()
            {
                this.gotStats = false;
                this.moves = 0;
                this.alive = false;
                this.whitelisted = false;
                this.teamId = 0;
                this.squadId = 0;
                this.targetTeam = 0;
                this.targetSquad = 0;
                this.rank = 0;
                this.spm = 0;
                this.skill = 0;
                this.name = "EmptySoldier";
                this.expires = DateTime.Now.AddSeconds(70);  // set join timeout
                this.timeJoined = DateTime.Now;
            }

            public xPlayer(string name)
            {
                this.gotStats = false;
                this.moves = 0;
                this.alive = false;
                this.whitelisted = false;
                this.teamId = 0;
                this.squadId = 0;
                this.targetTeam = 1;
                this.targetSquad = 0;
                this.rank = 30;
                this.spm = 230;
                this.skill = 125;
                this.name = name;
                this.expires = DateTime.Now.AddSeconds(70);  // set join timeout
                this.timeJoined = DateTime.Now;
            }

            /// <summary>
            /// Fetches the stats of the player from battlelog. Sets GotStats to true when successful.
            /// </summary>
            public void FetchStats()
            {
                try
                {
                    Console.WriteLine("Fetching stats for " + this.name + "...");

                    BattlelogClient bClient = new BattlelogClient();

                    Hashtable stats = new Hashtable(bClient.getStats(this.name));

                    if (stats.ContainsKey("tag"))
                    {
                        this.tag = Convert.ToString(stats["tag"]);
                    }

                    if (stats.ContainsKey("friends"))
                    {
                        this.friends = (string[])(stats["friends"]);
                    }

                    Hashtable overviewStats = new Hashtable((Hashtable)stats["overviewStats"]);

                    if (overviewStats.ContainsKey("rank"))
                    {
                        this.rank = Convert.ToInt32(overviewStats["rank"].ToString());
                    }

                    if (overviewStats.ContainsKey("scorePerMinute"))
                    {
                        this.spm = Convert.ToInt32(overviewStats["scorePerMinute"]);
                    }

                    if (overviewStats.ContainsKey("elo"))
                    {
                        this.skill = Convert.ToInt32(overviewStats["elo"]);
                    }

                    this.gotStats = true;

                    Console.WriteLine("Stat Fetch for " + name + " is done!");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception Caught in xPlayer.FetchStats");
                    Console.WriteLine(e.Message);
                    this.statsErrorMessage = e.Message;
                }
            }

            public string Name
            {
                get { return this.name; }
            }

            public string Tag
            {
                get { return this.tag; }
                set { this.tag = value; }
            }

            public string[] Friends
            {
                get { return this.friends; }
                set { this.friends = value; }
            }

            public int TeamId
            {
                get { return this.teamId; }
                set { this.teamId = value; }
            }

            public int SquadId
            {
                get { return this.squadId; }
                set { this.squadId = value; }
            }

            public int TargetTeam
            {
                get { return this.targetTeam; }
                set { this.targetTeam = value; }
            }

            public int TargetSquad
            {
                get { return this.targetSquad; }
                set { this.targetSquad = value; }
            }

            public int Rank
            {
                get { return this.rank; }
                set { this.rank = value; }
            }

            public int SPM
            {
                get { return this.spm; }
                set { this.spm = value; }
            }

            public int Skill
            {
                get { return this.skill; }
                set { this.skill = value; }
            }

            public int Dexterity
            {
                get { return this.spm; }
            }

            public bool GotStats
            {
                get { return this.gotStats; }
                set { this.gotStats = value; }
            }

            public int Moves
            {
                get { return this.moves; }
                set { this.moves = value; }
            }

            public bool Alive
            {
                get { return this.alive; }
                set { this.alive = value; }
            }

            public bool Whitelisted
            {
                get { return this.whitelisted; }
                set { this.whitelisted = value; }
            }

            public DateTime Expires
            {
                get { return this.expires; }
                set { this.expires = value; }
            }

            public DateTime TimeJoined
            {
                get { return this.timeJoined; }
                set { this.timeJoined = value; }
            }

            public string StatsError
            {
                get { return this.statsErrorMessage; }
                set { this.statsErrorMessage = value; }
            }

            public int CompareTo(xPlayer other)
            {
                return this.Dexterity.CompareTo(other.Dexterity);
            }
        }

        public class BattlelogClient
        {
            WebClient client = null;

            private String fetchWebPage(ref String html_data, String url)
            {
                try
                {
                    if (client == null)
                        client = new WebClient();

                    html_data = client.DownloadString(url);
                }
                catch (WebException e)
                {
                    if (e.Status.Equals(WebExceptionStatus.Timeout))
                        throw new Exception("HTTP request timed-out");
                    else
                        throw;

                }
                return html_data;
            }

            public Hashtable getStats(String player)
            {
                try
                {
                    /* First fetch the player's main page to get the persona id */
                    String result = "";
                    fetchWebPage(ref result, "http://battlelog.battlefield.com/bf3/user/" + player);

                    String tag = extractClanTag(result, player);

                    /* Extract the persona id */
                    MatchCollection pid = Regex.Matches(result, @"bf3/soldier/" + player + @"/stats/(\d+)(/\w*)?/", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    String personaId = "";

                    foreach(Match m in pid)
                    {
                        if (m.Success && m.Groups[2].Value.Trim() != "/ps3" && m.Groups[2].Value.Trim() != "/xbox")
                        {
                            personaId = m.Groups[1].Value.Trim();
                        }
                    }

                    if (personaId == "")
                        throw new Exception("could not find persona-id for ^b" + player);

                    fetchWebPage(ref result, "http://battlelog.battlefield.com/bf3/overviewPopulateStats/" + personaId + "/bf3-us-engineer/1/");

                    Hashtable json = (Hashtable)JSON.JsonDecode(result);

                    // check we got a valid response
                    if (!(json.ContainsKey("type") && json.ContainsKey("message")))
                        throw new Exception("JSON response does not contain \"type\" or \"message\" fields");

                    String type = (String)json["type"];
                    String message = (String)json["message"];

                    /* verify we got a success message */
                    if (!(type.StartsWith("success") && message.StartsWith("OK")))
                        throw new Exception("JSON response was type=" + type + ", message=" + message);

                    /* verify there is data structure */
                    Hashtable data = null;
                    if (!json.ContainsKey("data") || (data = (Hashtable)json["data"]) == null)
                        throw new Exception("JSON response was does not contain a data field");

                    string[] friends = GetFriends(player);
                    data.Add("friends", friends);
                    data.Add("tag", tag);
                    return data;
                }
                catch (Exception e)
                {
                    Console.WriteLine("getStats failed beacause: " + e.Message);
                    //Handle exceptions here however you want
                }

                return null;
            }

            public String extractClanTag(String result, String player)
            {
                /* Extract the player tag */
                Match tag = Regex.Match(result, @"\[\s*([a-zA-Z0-9]+)\s*\]\s*" + player, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (tag.Success)
                    return tag.Groups[1].Value;

                return String.Empty;
            }

            public String[] GetFriends(String player)
            {
                try
                {
                    String result = "";
                    fetchWebPage(ref result, "http://battlelog.battlefield.com/bf3/user/" + player + "/friends/");

                    MatchCollection friends = Regex.Matches(result, @"<a class=""base-profile-link"" href=""/bf3/user/(.+?)/"">.+?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    List<string> friendlist = new List<string>();
                    foreach (Match m in friends)
                    {
                        if (m.Success)
                        {
                            friendlist.Add(m.Groups[1].Value.Trim());
                            //Console.WriteLine(player + " has a friend " + m.Groups[1].Value.Trim());
                        }
                    }
                    string[] friendarray = friendlist.ToArray();
                    return friendarray;
                }
                catch (Exception e)
                {
                    Console.WriteLine("GetFriends failed beacause: " + e.Message);
                }
                return new string[1];
            }
        }

        public class TeamChange
        {
            string name = "";
            int startTeam = 0;
            int startSquad = 0;
            int endTeam = 0;
            int endSquad = 0;
            bool adminMoved = false;
            DateTime expires = DateTime.Now.AddSeconds(0.9);
            public TeamChange(string name, int startTeam, int startSquad)
            {
                this.name = name;
                this.startTeam = startTeam;
                this.startSquad = startSquad;
            }

            public TeamChange(string name, int startTeam, int startSquad, int endTeam, int endSquad)
            {
                this.name = name;
                this.startTeam = startTeam;
                this.startSquad = startSquad;
                this.endTeam = endTeam;
                this.endSquad = endSquad;
            }

            public string Name
            {
                get { return this.name; }
            }

            public int StartTeam
            {
                get { return this.startTeam; }
            }

            public int StartSquad
            {
                get { return this.startSquad; }
            }

            public int EndTeam
            {
                get { return this.endTeam; }
                set { this.endTeam = value; }
            }

            public int EndSquad
            {
                get { return this.endSquad; }
                set { this.endSquad = value; }
            }

            public bool AdminMoved
            {
                get { return this.adminMoved; }
                set { this.adminMoved = value; }
            }

            public bool Expired
            {
                get { return (this.expires < DateTime.Now); }
            }
        }

        #endregion
    }
}