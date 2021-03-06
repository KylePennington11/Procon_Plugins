
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
//using System.Drawing;


using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;

namespace PRoConEvents {
    public class xVotemap : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables and Constructors
        private string m_strHostName;
        private string m_strPort;
        private string m_strPRoConVersion;

        // User Settings
        private int m_iVoteThres = 0;
        private int m_iRandomness = 5;
        private int m_iNumOfMapOptions = 4;
        private int m_iVotingDuration = 240;
        private int m_iStopVoteTime = 180;
        private int m_iRushVoteStartTime = 600;  // in seconds
        private int minimumPlayers = 0;
        private int maximumPlayers = 64;

        private string m_strTrigger = "Automatic";
        private string m_strEnableVoteBanner = "First";
        private string m_strBannerType = "Yell";
        private int m_iBannerYellDuration = 5;
        private enumBoolYesNo m_enumExcludeCurrMap = enumBoolYesNo.Yes;
        private int m_iVotingOptionsInterval = 60;       
        private int m_iNextMapDisplayInterval = 600;
        private enumBoolYesNo m_enumShowGamemode = enumBoolYesNo.No;

        private int m_iDebugLevel = 3;

        // The Rest
        private List<TeamScore> m_listCurrTeamScore = new List<TeamScore>();
        //private List<CPlayerInfo> m_listCurrPlayers = new List<CPlayerInfo>();
        private List<MaplistEntry> m_listCurrMapList = new List<MaplistEntry>();
        private Dictionary<string, int> m_dictVoting = new Dictionary<string, int>();     // Player Name, Vote
        private List<string> m_listMapOptions;            // Map name
        private List<string> m_listGamemodeOptions;       // gamemode
        private List<MaplistEntry> m_listPastMaps = new List<MaplistEntry>();
        private int m_iCurrPlayerCount = 0;
        private DateTime m_timeServerInfoCheck = DateTime.Now;

        private string mapSortSettings = "Map and Gametype";
        private string m_strCurrentGameMode = "";
        private string m_strCurrentMap = "";
        private string m_strNextMap = "";
        private string m_strNextMode = "";
        private int m_iNextMapIndex = -1;
        private int m_iCurrMapIndex = -1;
        private int m_iCurrentRoundTime = 0;
        private Players m_players = new Players();
        
        private List<string> m_listOptsDisplay;

        private bool m_boolVotemapEnabled = true;
        private bool m_boolVotingStarted = false;
        private bool m_boolVotingSystemEnabled = false;
        private bool m_boolOnLastRound = true;
        private enumBoolYesNo showNextMapBeforeVote = enumBoolYesNo.No;
        private bool nextmapShown = false;
        private enumBoolYesNo disableVoteResults = enumBoolYesNo.No;
        

        //string m_strLastMessage = "";

        private DateTime m_timeVoteStart;
        private DateTime m_timeVoteEnd;
        private int m_iEndTimeLeeway = 60;  // seconds
        private TimeSpan m_tsStartVote = TimeSpan.FromHours(1);

        private DateTime m_timePrevious = DateTime.Now;
        private int[] m_iPrevTicket;
        private int ticketCounter = 100;
        private int currentMcom = 0;

        private Dictionary<char, double> m_dictLetterSizes = new Dictionary<char, double>();
 
        private class Players
        {
            private List<Player> m_listPlayers = new List<Player>();

            public void UpdatePlayer(CPlayerInfo player)
            {
                bool updated = false;
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == player.SoldierName)
                    {
                        p.TeamID = player.TeamID;
                        p.SquadID = player.SquadID;
                        updated = true;
                        break;
                    }
                }
                if (!updated)
                {
                    Add(player.SoldierName, player.TeamID, player.SquadID);
                }
            }

            public bool SetVote(string name, int vote) 
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        p.Vote = vote;
                        return true;
                    }
                }
                return false;
            }

            public Player GetPlayer(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        return p;
                    }
                }
                return null;
            }

            public List<Squad> GetNonvotedSquads()
            {
                List<Squad> squads = new List<Squad>();

                foreach (Player p in m_listPlayers)
                {
                    if (p.Vote == -1 && !(p.TeamID == 0 && p.SquadID == 0))
                    {
                        bool found = false;
                        foreach (Squad s in squads)
                        {
                            if (s.TeamID == p.TeamID && s.SquadID == p.SquadID)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            squads.Add(new Squad(p.TeamID, p.SquadID));
                        }
                    }
                }

                squads.Sort();

                return squads;
            }

            public void Add(string name, int teamId, int squadId)
            {
                if (!this.Contains(name))
                {
                    m_listPlayers.Add(new Player(name, teamId, squadId));
                }
            }

            public void Remove(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        m_listPlayers.Remove(p);
                    }
                }
            }

            public int Count
            {
                get { return m_listPlayers.Count; }
            }

            public bool Contains(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        return true;
                    }
                }
                return false;
            }

            public class Player
            {
                private string m_Name = "";
                private int m_TeamId = -1;
                private int m_SquadId = -1;
                private int m_Vote = -1;

                public Player(string name, int teamId, int squadId)
                {
                    m_Name = name;
                    m_TeamId = teamId;
                    m_SquadId = squadId;
                    m_Vote = -1;
                }

                public string SoldierName { 
                    get {return m_Name;}
                    private set{m_Name = value;}
                }

                public int TeamID {
                    get { return m_TeamId; }
                    set { m_TeamId = value; }
                }

                public int SquadID {
                    get { return m_SquadId; }
                    set { m_SquadId = value; }
                }

                public int Vote {
                    get { return m_Vote; }
                    set { m_Vote = value; }
                }

            }

            public class Squad : IComparable<Squad>
            {
                private int m_TeamId = -1;
                private int m_SquadId = -1;
                public Squad(int team, int squad)
                {
                    m_TeamId = team;
                    m_SquadId = squad;
                }

                public int TeamID
                {
                    get { return m_TeamId; }
                }

                public int SquadID
                {
                    get { return m_SquadId; }
                }

                public int CompareTo(Squad other)
                {
                    if (this.TeamID == other.TeamID)
                    {
                        return this.SquadID.CompareTo(other.SquadID);
                    }
                    return this.TeamID.CompareTo(other.TeamID);
                }
            }
        }

        public xVotemap()
        {
            #region Char Dictionary
            m_dictLetterSizes.Add(' ',0.50);
            m_dictLetterSizes.Add('a',0.90);
            m_dictLetterSizes.Add('b',0.90);
            m_dictLetterSizes.Add('c',0.78);
            m_dictLetterSizes.Add('d',0.90);
            m_dictLetterSizes.Add('e',0.90);
            m_dictLetterSizes.Add('f',0.56);
            m_dictLetterSizes.Add('g',0.90);
            m_dictLetterSizes.Add('h',0.90);
            m_dictLetterSizes.Add('i',0.40);
            m_dictLetterSizes.Add('j',0.50);
            m_dictLetterSizes.Add('k',0.86);
            m_dictLetterSizes.Add('l',0.40);
            m_dictLetterSizes.Add('m',1.40);
            m_dictLetterSizes.Add('n',0.90);
            m_dictLetterSizes.Add('o',0.90);
            m_dictLetterSizes.Add('p',0.90);
            m_dictLetterSizes.Add('q',0.90);
            m_dictLetterSizes.Add('r',0.62);
            m_dictLetterSizes.Add('s',0.76);
            m_dictLetterSizes.Add('t',0.55);
            m_dictLetterSizes.Add('u',0.90);
            m_dictLetterSizes.Add('v',0.85);
            m_dictLetterSizes.Add('w',1.28);
            m_dictLetterSizes.Add('x',0.82);
            m_dictLetterSizes.Add('y',0.85);
            m_dictLetterSizes.Add('z',0.78);
            m_dictLetterSizes.Add('A',1.00);
            m_dictLetterSizes.Add('B',1.00);
            m_dictLetterSizes.Add('C',1.00);
            m_dictLetterSizes.Add('D',1.12);
            m_dictLetterSizes.Add('E',1.00);
            m_dictLetterSizes.Add('F',0.90);
            m_dictLetterSizes.Add('G',1.18);
            m_dictLetterSizes.Add('H',1.12);
            m_dictLetterSizes.Add('I',0.62);
            m_dictLetterSizes.Add('J',0.70);
            m_dictLetterSizes.Add('K',1.00);
            m_dictLetterSizes.Add('L',0.85);
            m_dictLetterSizes.Add('M',1.32);
            m_dictLetterSizes.Add('N',1.11);
            m_dictLetterSizes.Add('O',1.20);
            m_dictLetterSizes.Add('P',0.92);
            m_dictLetterSizes.Add('Q',1.20);
            m_dictLetterSizes.Add('R',1.05);
            m_dictLetterSizes.Add('S',0.91);
            m_dictLetterSizes.Add('T',1.00);
            m_dictLetterSizes.Add('U',1.12);
            m_dictLetterSizes.Add('V',1.00);
            m_dictLetterSizes.Add('W',1.55);
            m_dictLetterSizes.Add('X',1.00);
            m_dictLetterSizes.Add('Y',1.00);
            m_dictLetterSizes.Add('Z',0.92);      
        #endregion
        }

        #endregion

        #region PluginSetup

        public string GetPluginName() {
            return "xVotemap";
        }

        public string GetPluginVersion() {
            return "0.1.3.1";
        }

        public string GetPluginAuthor() {
            return "aether";
        }

        public string GetPluginWebsite() {
            return "";
        }

        public string GetPluginDescription() {
            return @"

        <h2>Description</h2>
        <p>Calls an in-game vote for the next map near the end on the round. Randomly selects four maps (by default, can be adjusted) from the current maplist to vote on. Before the options are displayed, a banner can be shown to get the players attention that voting is about to commence.<br>

        The votes are tallied and if there are more votes than the set threshold, the winner is the map with the most votes. If there is a tie, the winner is selected at random from the tied maps. The next map is announced and it can be periodically displayed until the end of the round.</p>

        <h2>In-game Commands</h2><br>

        <blockquote> 
        <h4>/#</h4>While a vote is in progress, votes for the option represented by the #.
        </blockquote>

        <blockquote> 
        <h4>/v</h4>Displays the vote options while a vote is in progress. If typed before a vote, it will display the predicted amount of time before the vote begins.
        </blockquote>

        <blockquote> 
        <h4>/nextmap</h4>Displays the next map.
        </blockquote>

        <blockquote> 
        <h4>/votemap</h4>Initiates/restarts a votemap if the player has map changing priviledges in Procon.
        </blockquote>

        <blockquote> 
        <h4>enablevotemap</h4>Enables the votemap plugin if the player has map changing priviledges in Procon.
        </blockquote>

        <blockquote> 
        <h4>disablevotemap</h4>Disables the votemap plugin if the player has map changing priviledges in Procon.
        </blockquote>

        <h2>Settings</h2><br>

        <h3>Display</h3><br>

        <blockquote> 
        <h4>Enable Vote Banner?</h4>Is the banner (designed to get the attention of players) displayed before the first voting options list (First), before every map option display (All), or never displayed (disabled).
        </blockquote>

        <blockquote> 
        <h4>Show Gamemode in Voting Options?</h4> Determines if the shorthand notation for the gamemode is displayed in the voting options. This is most useful for those running mixed mode servers, especially if the same map is in the maplist with different gamemodes.
        </blockquote>

        <blockquote> 
        <h4>Show Nextmap Before Vote?</h4> If 'yes', then the default next map is shown before the vote options so people can decide if they want to vote or not.
        </blockquote>

        <blockquote> 
        <h4>Voting Options Interval</h4>Integer, in seconds, representing how often the map vote options are displayed once the voting poll opens.
        </blockquote>

        <blockquote> 
        <h4>Next Map Display Interval</h4>Integer, in seconds, representing how often the winning next map is displayed after the voting is over but the round hasn't ended yet. A value of [b]-1[/b] will disable nextmap messages.
        </blockquote>

        <blockquote> 
        <h4|Disable Vote Results Display?</h4>If 'yes' then the results of the vote will NOT be displayed. It will still say when voting ends or if the vote failed.
        </blockquote>

        <h3>Map Options</h3><br>
        
        <blockquote> 
        <h4>Number of Vote Options</h4>Integer, between 2 and 8, representing the number of vote options to be displayed when a mapvote is called.
        </blockquote>

        <blockquote> 
        <h4>Exclude Current Map From  Vote Options?</h4>If yes, the current map with be excluded from the next map voting options, so to not allow the same map to be played twice.
        </blockquote>

        <blockquote> 
        <h4>Sort the Maplist by:</h4>If 'Map Only' is selected, when a map is played all the gametypes for that map will be pushed to the end of the list. 'Map and Gametype' will only push that particular map and gametpye to the end of the list. 'Gametype Only' will push all maps with that gametype to the end of the list.
        </blockquote>

        <blockquote> 
        <h4>Randomness</h4>An integer between 0 and 10 representing the randomness of the map options for a vote. The algorithm changes the probability of each map and gamemode pair being selected as a vote option depending on how long ago it was last played.<br>
        eg. If the maplist has 13 maps and number of vote options is set to 4. The maplist is sorted from played a long time ago (1) to played last map (13).<br>
        <img border=""0"" src=""http://i.imgur.com/kCvSK.png""><br>
        Randomness = 0: Will rotate through the maplist, only showing the maps that haven't been played in a long time. Players should end up playing every map in the cycle.<br><br>
        <img border=""0"" src=""http://i.imgur.com/fZPGC.png""><br><br>
        <img border=""0"" src=""http://i.imgur.com/CmULh.png""><br>
        Randomness = 5: Maps that haven't been played in a long time will have the highest probablity and recently played maps will have the lowest probability of being selected as vote options.<br><br>
        <img border=""0"" src=""http://i.imgur.com/fHtpu.png""><br><br>
        <img border=""0"" src=""http://i.imgur.com/nlYTd.png""><br>
        Randomness = 10: Every map has the same chance of being selected as an option. Players will tend to play the most popular maps repeatedly.<br><br>
        </blockquote>

        <h3>Voting</h3><br>      

        <blockquote> 
        <h4>Trigger</h4>If Automatic, the voting will be triggered automatically near the end of the round. If Manual, the voting will have to be triggered manually with the /votemap command.
        </blockquote>

        <blockquote> 
        <h4>Voting Threshold</h4>Integer, representing the minimum total number of votes that must cast for the highest voted map to become the nextmap. If no map reaches this threshold, then the map will just roll over to the next map in the maplist.
        </blockquote>

        <blockquote> 
        <h4>Voting Duration</h4>Integer, in seconds, representing the target length of time the voting poll is open for.
        </blockquote>

        <blockquote> 
        <h4>Time between Voting End and Round End</h4>Integer, in seconds, representing how long from the end of the round the voting will finish. In other words, at what time you want the voting to take place, mear seconds before the end of round or in the middle of round sometime.
        </blockquote>

        <blockquote> 
        <h4>Rush Voting Start Time from Start of Round</h4>Integer, representing the time when voting will start, in seconds from the start of the round. In rush, there is no way to predict when the round will finish with the current RCON information, as the status of MCOMs are unknown. So you must estimate how long the shortest rush round would take and start the vote a couple of minutes earlier in order to ensure a vote will take place.
        </blockquote>

        <blockquote> 
        <h4>Minimum Players for Start</h4>Integer, representing the minimum number for players required in the server at the time of vote for the vote to start.
        </blockquote>

        <blockquote> 
        <h4>Maximum Players for Start</h4>Integer, representing the maximum number for players allowed in the server at the time of vote for the vote to start.
        </blockquote>

        <h3>Xtras</h3><br>

        <blockquote> 
        <h4>Debug Level</h4>Integer between 0 and 5, where 0 outputs no plugin debug messages, and 5 which outputs even the most mundane steps.
        </blockquote>

        <h2>Development</h2><br>
        
        <h3>Known issues</h3><br>
        <ul>
        <li>If the maplist is changed during a vote the winning map will not correctly be the next map.</li>
        </ul>

        <h3>Future Work</h3><br>
        <ul>
        <li>Improve map sorting algorithm/li>
        <li>Add maplist change event</li>
        </ul>

        <h3>Change Log</h3><br>
        <h4>1.3.1</h4><br>
        <ul>
        <li>Fixed: /votemap to start a map vote.</li>
        </ul>
        <h4>1.3.0</h4><br>
        <ul>
        <li>Added: Yell announcement.</li>
        <li>Added: In-game enable and disable.</li>
        </ul>
        <h4>1.2.3</h4><br>
        <ul>
        <li>Fixed: Mapnames fixed from patch.</li>
        </ul>
        <h4>1.2.2</h4><br>
        <ul>
        <li>Fixed: Maplist sort for 'Map Only' and 'Gametype Only'. Will still work on a better algorithm too.</li>
        </ul>
        <h4>1.2.1</h4><br>
        <ul>
        <li>Added: Information link with Ultimate Map Manager to ensure the voted map will be the next map if the maplist needs to change and it is still in the maplist.</li>
        <li>Fixed: Remove current map bug.</li>
        <li>Added: Improved map options display. Should reduce the chance of very long options.</li>
        <li>Fixed: Gamemode should appear throughout the in-game message, if turned on.</li>
        </ul>
        <h4>1.2.0</h4><br>
        <ul>
        <li>Added: Ability to disable vote results from being displayed.</li>
        <li>Added: Option to change the way the maplist is sorted.</li>
        <li>Added: Option to display the nextmap before the vote.</li>
        <li>Added: Option to set minimum and maximum player count limits for if the vote will take place.</li>
        </ul>
        <h4>1.1.5</h4><br>
        <ul>
        <li>Added: More debug messages</li>
        <li>Fix: Serverinfo bug possibly =\</li>
        <li>Change: Renamed to xVotemap. Delete old CxVotemap.</li>
        </ul>
        <h4>1.1.4</h4><br>
        <ul>
        <li>Fix: /votemap command now works on all rounds</li>
        <li>Fix: Between rounds error bug.</li>
        </ul>
        <h4>1.1.3</h4><br>
        <ul>
        <li>Fix: Votemap starting too early.</li>
        </ul>
        <h4>1.1.2</h4><br>
        <ul>
        <li>Fix: Minimal changes</li>
        </ul>
        <h4>1.1.1</h4><br>
        <ul>
        <li>Fix: Several bugs that were in the new randomness code. A lot of console debug text has been added, most will be removed or suppressed when this version is proved stable.</li>
        </ul>
        <h4>1.1.0</h4><br>
        <ul>
        <li>Added: Randomness, a new algorithm to decide which maps are chosen to be vote options.</li>
        <li>Added: Ability to disable votemap starting automatically.</li>
        </ul>
        <h4>1.0.1</h4><br>
        <ul>
        <li>Fixed: Tweaked various algorithms</li>
        <li>Fixed: Squad rush bug</li>
        </ul>
        <h4>1.0.0</h4><br>
        <ul>
        <li>Added: Warning for incorrect vote command</li>
        </ul>
        <h4>0.0.8</h4><br>
        <ul>
        <li>Fix: Exclude current map</li>
        <li>Fix: Change to rush start time estimate. Needs testing again.</li>
        </ul>
        <h4>0.0.7</h4><br>
        <ul>
        <li>Fix: Mixed-mode bug. Needs testing to check if it succeeded.</li>
        <li>Added: Customizable number of vote options</li>
        <li>Added: Option to display the gamemode in the vote options</li>
        <li>Added: Admins with mapchanging privileges can initiate/restart the Votemap in-game with /votemap </li>
        </ul>
        <h4>0.0.6</h4><br>
        <ul>
        <li>Fix: Bug where the last map in the maplist would not be selected.</li>
        <li>Added: /nextmap command</li>
        </ul>
        <h4>0.0.5</h4><br>
        <ul>
        <li>Fixed, Last round bug</li>
        </ul>
        <h4>0.0.4</h4><br>
        <ul>
        <li>Added, 'Exclude current map' as an option in the settings.</li>
        <li>Fixed, xVotemap stopping due to a rush map being detected.</li>
        </ul>
        <h4>0.0.3</h4><br>
        <ul>
        <li>Changed the vote threshold to be compared to the total votes cast, rather than the winning map.</li>
        <li>In the event of a draw, now, a random map out of the drawn maps will be chosen.</li>
        <li>Added Rush and Deathmatch support, expect bugs, please report bugs and provide feedback.</li>
        </ul>
        <h4>0.0.2</h4><br>
        <ul>
        <li>Fix for vote banner not showing.</li>
        </ul>
        <h4>0.0.1</h4><br>
        <ul>
        <li>Initial Beta. Find all of the bugs! <img border=""0"" src=""http://i.imgur.com/88jZM.png"" width=""50"" height=""50""></li>
        </ul>
";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Display|Enable Vote Banner?", "enum.VoteBanner(Disabled|First|All)", this.m_strEnableVoteBanner));
            if (this.m_strEnableVoteBanner == "First" || this.m_strEnableVoteBanner == "All")
            {
                lstReturn.Add(new CPluginVariable("Display|Banner Type", "enum.VoteBannerType(Chat|Yell|Both)", this.m_strBannerType));

                if (this.m_strBannerType == "Yell" || this.m_strBannerType == "Both")
                {
                    lstReturn.Add(new CPluginVariable("Display|Banner Yell Duration (s)", this.m_iBannerYellDuration.GetType(), this.m_iBannerYellDuration));
                }
            }

            lstReturn.Add(new CPluginVariable("Display|Voting Options Interval (s)", this.m_iVotingOptionsInterval.GetType(), this.m_iVotingOptionsInterval));
            lstReturn.Add(new CPluginVariable("Display|Show Nextmap Before Vote?", typeof(enumBoolYesNo), this.showNextMapBeforeVote));
            lstReturn.Add(new CPluginVariable("Display|Next Map Display Interval (s)", this.m_iNextMapDisplayInterval.GetType(), this.m_iNextMapDisplayInterval));
            lstReturn.Add(new CPluginVariable("Display|Show Gamemode in Voting Options?", typeof(enumBoolYesNo), this.m_enumShowGamemode));
            lstReturn.Add(new CPluginVariable("Display|Disable Vote Results Display?", typeof(enumBoolYesNo), this.disableVoteResults));

            lstReturn.Add(new CPluginVariable("Map Options|Number of Vote Options", this.m_iNumOfMapOptions.GetType(), this.m_iNumOfMapOptions));
            lstReturn.Add(new CPluginVariable("Map Options|Exclude Current Map From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMap));
            lstReturn.Add(new CPluginVariable("Map Options|Sort the Maplist by:", "enum.MapGametype(Map Only|Map and Gametype|Gametype Only)", this.mapSortSettings));
            lstReturn.Add(new CPluginVariable("Map Options|Randomness", this.m_iRandomness.GetType(), this.m_iRandomness));

            lstReturn.Add(new CPluginVariable("Voting|Trigger", "enum.VotingTrigger(Manual|Automatic)", this.m_strTrigger));
            lstReturn.Add(new CPluginVariable("Voting|Vote Threshold (votes)", this.m_iVoteThres.GetType(), this.m_iVoteThres));
            lstReturn.Add(new CPluginVariable("Voting|Voting Duration (s)", this.m_iVotingDuration.GetType(), this.m_iVotingDuration));
            lstReturn.Add(new CPluginVariable("Voting|Time between Voting End and Round End (s)", this.m_iStopVoteTime.GetType(), this.m_iStopVoteTime));
            lstReturn.Add(new CPluginVariable("Voting|Rush Voting Start Time from Start of Round (s)", this.m_iRushVoteStartTime.GetType(), this.m_iRushVoteStartTime));
            lstReturn.Add(new CPluginVariable("Voting|Minimum Players for Start", this.minimumPlayers.GetType(), this.minimumPlayers));
            lstReturn.Add(new CPluginVariable("Voting|Maximum Players for Start", this.maximumPlayers.GetType(), this.maximumPlayers));

            lstReturn.Add(new CPluginVariable("Xtras|Debug Level", this.m_iDebugLevel.GetType(), this.m_iDebugLevel));

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            lstReturn.Add(new CPluginVariable("Enable Vote Banner?", "enum.VoteBanner(Disabled|First|All)", this.m_strEnableVoteBanner));
            lstReturn.Add(new CPluginVariable("Banner Type", "enum.VoteBannerType(Chat|Yell|Both)", this.m_strBannerType));
            lstReturn.Add(new CPluginVariable("Banner Yell Duration (s)", this.m_iBannerYellDuration.GetType(), this.m_iBannerYellDuration));
            lstReturn.Add(new CPluginVariable("Voting Options Interval (s)", this.m_iVotingOptionsInterval.GetType(), this.m_iVotingOptionsInterval));
            lstReturn.Add(new CPluginVariable("Show Nextmap Before Vote?", typeof(enumBoolYesNo), this.showNextMapBeforeVote));
            lstReturn.Add(new CPluginVariable("Next Map Display Interval (s)", this.m_iNextMapDisplayInterval.GetType(), this.m_iNextMapDisplayInterval));
            lstReturn.Add(new CPluginVariable("Show Gamemode in Voting Options?", typeof(enumBoolYesNo), this.m_enumShowGamemode));
            lstReturn.Add(new CPluginVariable("Disable Vote Results Display?", typeof(enumBoolYesNo), this.disableVoteResults));

            lstReturn.Add(new CPluginVariable("Number of Vote Options", this.m_iNumOfMapOptions.GetType(), this.m_iNumOfMapOptions));
            lstReturn.Add(new CPluginVariable("Exclude Current Map From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMap));
            lstReturn.Add(new CPluginVariable("Sort the Maplist by:", "enum.MapGametype(Map Only|Map and Gametype|Gametype Only)", this.mapSortSettings));
            lstReturn.Add(new CPluginVariable("Randomness", this.m_iRandomness.GetType(), this.m_iRandomness));

            lstReturn.Add(new CPluginVariable("Trigger", "enum.VotingTrigger(Manual|Automatic)", this.m_strTrigger));
            lstReturn.Add(new CPluginVariable("Vote Threshold (votes)", this.m_iVoteThres.GetType(), this.m_iVoteThres));
            lstReturn.Add(new CPluginVariable("Voting Duration (s)", this.m_iVotingDuration.GetType(), this.m_iVotingDuration));
            lstReturn.Add(new CPluginVariable("Time between Voting End and Round End (s)", this.m_iStopVoteTime.GetType(), this.m_iStopVoteTime));
            lstReturn.Add(new CPluginVariable("Rush Voting Start Time from Start of Round (s)", this.m_iRushVoteStartTime.GetType(), this.m_iRushVoteStartTime));
            lstReturn.Add(new CPluginVariable("Minimum Players for Start", this.minimumPlayers.GetType(), this.minimumPlayers));
            lstReturn.Add(new CPluginVariable("Maximum Players for Start", this.maximumPlayers.GetType(), this.maximumPlayers));

            lstReturn.Add(new CPluginVariable("Debug Level", this.m_iDebugLevel.GetType(), this.m_iDebugLevel));

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            int iValue = 0;

            if (strVariable.CompareTo("Exclude Current Map From Vote Options?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumExcludeCurrMap = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Randomness") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iRandomness = iValue;

                if (iValue < 0)
                {
                    this.m_iRandomness = 0;
                }
                else if (iValue > 10)
                {
                    this.m_iRandomness = 10;
                }
            }
            else if (strVariable.CompareTo("Sort the Maplist by:") == 0)
            {
                this.mapSortSettings = strValue;
            }
            else if (strVariable.CompareTo("Number of Vote Options") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iNumOfMapOptions = iValue;

                if (iValue < 2)
                {
                    this.m_iNumOfMapOptions = 2;
                }
                else if (iValue > 8)
                {
                    this.m_iNumOfMapOptions = 8;
                }
            }
            else if (strVariable.CompareTo("Voting Duration (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVotingDuration = iValue;

                if (iValue < 30)
                {
                    this.m_iVotingDuration = 30;
                }
            }
            else if (strVariable.CompareTo("Time between Voting End and Round End (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iStopVoteTime = iValue;

                if (iValue < 90)
                {
                    this.m_iStopVoteTime = 90;
                }
            }
            else if (strVariable.CompareTo("Voting Options Interval (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVotingOptionsInterval = iValue;

                if (iValue < 10)
                {
                    this.m_iVotingOptionsInterval = 10;
                }
            }
            if (strVariable.CompareTo("Show Nextmap Before Vote?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showNextMapBeforeVote = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Next Map Display Interval (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iNextMapDisplayInterval = iValue;

                if (iValue < -1)
                {
                    this.m_iNextMapDisplayInterval = -1;
                }
            }
            else if (strVariable.CompareTo("Trigger") == 0)
            {
                this.m_strTrigger = strValue;
            }
            else if (strVariable.CompareTo("Vote Threshold (votes)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVoteThres = iValue;

                if (iValue < 1)
                {
                    this.m_iVoteThres = 0;
                }
                else if (iValue > 64)
                {
                    this.m_iVoteThres = 64;
                }
            }
            else if (strVariable.CompareTo("Rush Voting Start Time from Start of Round (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iRushVoteStartTime = iValue;

                if (iValue < 1)
                {
                    this.m_iRushVoteStartTime = 1;
                }
            }
            else if (strVariable.CompareTo("Minimum Players for Start") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                minimumPlayers = iValue;
            }
            else if (strVariable.CompareTo("Maximum Players for Start") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                maximumPlayers = iValue;
            }
            else if (strVariable.CompareTo("Enable Vote Banner?") == 0)
            {
                this.m_strEnableVoteBanner = strValue;
            }
            else if (strVariable.CompareTo("Banner Type") == 0)
            {
                this.m_strBannerType = strValue;
            }
            else if (strVariable.CompareTo("Banner Yell Duration (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                m_iBannerYellDuration = iValue;
            }
            else if (strVariable.CompareTo("Show Gamemode in Voting Options?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumShowGamemode = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Disable Vote Results Display?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.disableVoteResults = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Debug Level") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                m_iDebugLevel = iValue;
            }

        }

        #endregion

        #region Procon Events

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.m_strHostName = strHostName;
            this.m_strPort = strPort;
            this.m_strPRoConVersion = strPRoConVersion;
            this.RegisterEvents(this.GetType().Name, "OnListPlayers", "OnPlayerLeft", "OnServerInfo", "OnGameModeCounter", "OnMaplistList", "OnMaplistGetMapIndices", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOver", "OnLevelLoaded", "OnRestartLevel", "OnEndRound", "OnRunNextLevel");        
        }

        public void OnPluginEnable() {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^bxVotemap ^2Enabled!");

            StartVotingSystem();
        }

        public void OnPluginDisable() {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^bxVotemap ^1Disabled =(");

            StopVotingSystem();
        }

        public void OnListPlayers(List<CPlayerInfo> lstPlayers, CPlayerSubset cpsSubset)
        {          
            this.m_iCurrPlayerCount = lstPlayers.Count;
            foreach(CPlayerInfo player in lstPlayers)
            {
                m_players.UpdatePlayer(player);
            }
            WritePluginConsole("There are " + m_players.Count + " players in the db.", "Info", 5);
        }

        public void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            m_players.Remove(playerInfo.SoldierName);
            WritePluginConsole(playerInfo.SoldierName + " removed from the db.", "Info", 5);
        }

        public void OnServerInfo(CServerInfo csiServerInfo) {
            try
            {
                if (this.m_timeServerInfoCheck < DateTime.Now && m_boolVotingSystemEnabled)
                {
                    WritePluginConsole("ServerInfo updating.", "Info", 5);
                    this.m_iCurrentRoundTime = csiServerInfo.RoundTime;
                    this.m_strCurrentMap = csiServerInfo.Map;
                    this.m_strCurrentGameMode = csiServerInfo.GameMode;
                    this.m_listCurrTeamScore = new List<TeamScore>(csiServerInfo.TeamScores);
                    //this.m_iCurrPlayerCount = csiServerInfo.PlayerCount;

                    if (this.m_iPrevTicket == null || this.m_iPrevTicket.Length == 0)
                    {
                        this.m_iPrevTicket = new int[this.m_listCurrTeamScore.Count];
                        for (int i = 0; i < this.m_listCurrTeamScore.Count; i++)
                        {
                            this.m_iPrevTicket[i] = -1;
                        }
                    }

                    if (m_boolVotingSystemEnabled && csiServerInfo.CurrentRound + 1 != csiServerInfo.TotalRounds)
                    {
                        this.m_boolOnLastRound = false;
                        StopVotingSystem();
                        WritePluginConsole("Not last round. Stopping voting system.", "Info", 3);
                        WritePluginConsole("Curr round: " + csiServerInfo.CurrentRound, "Info", 5);
                        WritePluginConsole("Total rounds: " + csiServerInfo.TotalRounds, "Info", 5);
                    }

                    SetVoteStartAndEndTimes();


                    WritePluginConsole("Voting system enabled?: " + m_boolVotingSystemEnabled.ToString(), "Info", 5);
                    WritePluginConsole("Current player count is: " + csiServerInfo.PlayerCount, "Info", 5);
                    WritePluginConsole("Current game mode is: " + csiServerInfo.GameMode, "Info", 5);
                    WritePluginConsole("Current round time is: " + csiServerInfo.RoundTime, "Info", 5);
                    WritePluginConsole("Current number of teams is: " + csiServerInfo.TeamScores.Count, "Info", 5);
                    if (csiServerInfo.TeamScores.Count >= 2)
                    {
                        WritePluginConsole("Team 0 score: " + csiServerInfo.TeamScores[0].Score, "Info", 5);
                        WritePluginConsole("Team 1 score: " + csiServerInfo.TeamScores[1].Score, "Info", 5);
                        WritePluginConsole("Team 0 winningscore: " + csiServerInfo.TeamScores[0].WinningScore, "Info", 5);
                        WritePluginConsole("Team 1 winningscore: " + csiServerInfo.TeamScores[1].WinningScore, "Info", 5);
                    }

                    this.ExecuteCommand("procon.protected.send", "mapList.list");
                    this.ExecuteCommand("procon.protected.send", "mapList.getMapIndices");

                    this.m_timeServerInfoCheck = DateTime.Now.AddSeconds(20);

                    WritePluginConsole("ServerInfo updated!", "Info", 5);
                }
                else if (this.m_timeServerInfoCheck >= DateTime.Now)
                {
                    WritePluginConsole("ServerInfo called too soon, skipping this call.", "Info", 4);
                    WritePluginConsole(String.Format("Now: {0:G}", DateTime.Now), "Info", 5);
                    WritePluginConsole(String.Format("Next: {0:G}", m_timeServerInfoCheck), "Info", 5);
                }
                else
                {
                    WritePluginConsole("Skipping ServerInfo call because voting system is disabled.", "Info", 4);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in: OnServerInfo", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void OnGameModeCounter(int limit)
        {
            this.ticketCounter = limit;
        }

        public void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            try
            {
                if (!ListsEqual(lstMaplist, m_listCurrMapList) && m_listCurrMapList.Count > 0)
                {
                    WritePluginConsole("Maplist change detected", "Info", 2);
                    if (m_boolVotingStarted)
                    {
                        WritePluginConsole("Restarting vote due to maplist change", "Work", 1);
                        // StartVotingPoll();
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Caught Exception in ListsEqual", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
                throw;
            }
            this.m_listCurrMapList = new List<MaplistEntry>(lstMaplist);
            WritePluginConsole("Maplist updated. There are " + m_listCurrMapList.Count + " maps currently in the maplist", "Info", 5);
        }

        public void OnMaplistGetMapIndices(int mapIndex, int nextIndex) 
        {
            this.m_iCurrMapIndex = mapIndex;
            this.m_iNextMapIndex = nextIndex;

            if (this.m_strNextMap == "")
            {
                this.m_strNextMap = GetMapByFilename(m_listCurrMapList[nextIndex].MapFileName).PublicLevelName;
                this.m_strNextMode = ConvertGamemodeToShorthand(m_listCurrMapList[nextIndex].Gamemode);
            }
        }

        public void OnGlobalChat(string speaker, string message) {
            ProcessChatMessage(speaker, message);
        }

        public void OnTeamChat(string speaker, string message, int teamId) { 
            ProcessChatMessage(speaker, message);
        }

        public void OnSquadChat(string speaker, string message, int teamId, int squadId) {
            ProcessChatMessage(speaker, message);
        }

        public void OnRoundOver(int winningTeamId)
        {
            WritePluginConsole("Round Over: Stopping voting system", "Work", 3);
            if (m_boolVotingStarted && m_boolVotingSystemEnabled)
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
            else
            {
                StopVotingSystem();
            }
            
        }

        public void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal) {
            WritePluginConsole("Level loaded: ^6" + GetMapByFilename(mapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(Gamemode) + "^0, Round " + (roundsPlayed + 1).ToString() + " of " + roundsTotal.ToString(), "Info", 3);
            if (roundsPlayed + 1 == roundsTotal)
            {
                this.m_boolOnLastRound = true;
                WritePluginConsole("Level loaded: Last round detected, starting voting system", "Work", 3);
                StartVotingSystem();
            }
            else
            {
                this.m_boolOnLastRound = false;
                WritePluginConsole("Level loaded: Not last round, voting system will not be enabled", "Work", 3);
            }

            for (int i = 0; i < m_listPastMaps.Count; i++)
            {
                if (m_listPastMaps[i].MapFileName == mapFileName && m_listPastMaps[i].Gamemode == Gamemode)
                {
                    WritePluginConsole("Map and mode found in played maplist. Removing.", "Work", 5);
                    m_listPastMaps.RemoveAt(i);                 
                    break;
                }
            }
            m_listPastMaps.Add(new MaplistEntry(Gamemode, mapFileName, roundsTotal));

        }

        public void OnRestartLevel()
        {
            WritePluginConsole("Level Restarted: Stopping voting system", "Work", 3);
            StopVotingSystem();
        }

        public void OnRunNextLevel()
        {
            WritePluginConsole("Skipped Level: Stopping voting system", "Work", 3);
            if (m_boolVotingStarted && m_boolVotingSystemEnabled)
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
            else
            {
                StopVotingSystem();
            }
        }

        #endregion

        #region Votemap

        public void RunVotingLogic()
        {
            WritePluginConsole("Running Voting Logic", "Info", 5);

            if (m_boolVotemapEnabled && showNextMapBeforeVote == enumBoolYesNo.Yes && !this.nextmapShown && m_strTrigger == "Automatic" && m_boolOnLastRound && !m_boolVotingStarted && m_boolVotingSystemEnabled && DateTime.Now > m_timeVoteStart.AddMinutes(-1) && DateTime.Now < m_timeVoteEnd && m_iCurrPlayerCount >= minimumPlayers && m_iCurrPlayerCount <= maximumPlayers)
            {
                DisplayNextMap("all");
                this.nextmapShown = true;
            }

            if (m_boolVotemapEnabled && m_strTrigger == "Automatic" && m_boolOnLastRound && !m_boolVotingStarted && m_boolVotingSystemEnabled && DateTime.Now > m_timeVoteStart && DateTime.Now < m_timeVoteEnd && m_iCurrPlayerCount >= minimumPlayers && m_iCurrPlayerCount <= maximumPlayers)
            {
                StartVotingPoll();   
            }
            else if (m_boolVotemapEnabled && m_boolVotingStarted && m_boolVotingSystemEnabled && (DateTime.Now > m_timeVoteEnd || DateTime.Now > m_timeVoteStart.AddSeconds(m_iVotingDuration)))
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
        }

        private void StartVotingSystem(){
            m_boolVotingStarted = false;
            m_boolVotingSystemEnabled = true;

            m_timeVoteStart = DateTime.Now.AddHours(1);
            m_timeVoteEnd = DateTime.Now.AddHours(1);
            m_timePrevious = DateTime.Now;
            this.nextmapShown = false;
            this.m_iPrevTicket = null;
            this.currentMcom = 0;
            this.ticketCounter = 100;
          //  this.m_iPrevTicket[0] = -1;
            this.m_strNextMap = "";

            this.m_dictVoting = new Dictionary<string, int>();
            this.m_players = new Players();

            this.ExecuteCommand("procon.protected.send", "vars.gameModeCounter");

            this.ExecuteCommand("procon.protected.tasks.add", "taskRunVotingLogic", "0", "5", "-1", "procon.protected.plugins.call", "xVotemap", "RunVotingLogic");
        }

        private void StopVotingSystem(){
            m_boolVotingStarted = false;
            m_boolVotingSystemEnabled = false;

            m_timePrevious = DateTime.Now;
            this.m_iPrevTicket = null;
          //  this.m_iPrevTicket[0] = -1;

            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayVoteOptions");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskRunVotingLogic");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayNextMap");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayVoteBanner");
        }

        private void SetVoteStartAndEndTimes()
        {
            try
            {
                if (m_strCurrentGameMode.Contains("Conquest") || m_strCurrentGameMode.Contains("DeathMatch"))
                {
                    int winningScore = 0;
                    if (m_strCurrentGameMode.Contains("TeamDeathMatch"))
                    {
                        winningScore = ticketCounter;
                    }
                    else if (m_strCurrentGameMode.Contains("SquadDeathMatch"))
                    {
                        winningScore = ticketCounter/2;
                    }

                    int timeToEnd = 9999;
                    if (m_iPrevTicket != null && m_iPrevTicket[0] != -1)
                    {
                        TimeSpan tspan = DateTime.Now - m_timePrevious;
                        double[] timeRemaining = new double[m_listCurrTeamScore.Count];

                        for (int i = 0; i < m_listCurrTeamScore.Count; i++)
                        {
                            double iTicketDiff = (double)(m_listCurrTeamScore[i].Score - m_iPrevTicket[i]);
                            double dbTicketPerSec = iTicketDiff / tspan.TotalSeconds;

                            timeRemaining[i] = 9999;

                            if (dbTicketPerSec != 0)
                            {
                                timeRemaining[i] = Convert.ToDouble(winningScore - m_listCurrTeamScore[i].Score) / dbTicketPerSec - m_iStopVoteTime;
                            }
                        }

                        timeToEnd = Convert.ToInt32(GetMin(timeRemaining));

                        WritePluginConsole("Ticket Count: Team " + this.m_listCurrTeamScore[0].TeamID.ToString() + ": " + this.m_listCurrTeamScore[0].Score + "/" + this.m_listCurrTeamScore[0].WinningScore.ToString() + " | " + "Team " + this.m_listCurrTeamScore[1].TeamID.ToString() + ": " + this.m_listCurrTeamScore[1].Score + "/" + this.m_listCurrTeamScore[1].WinningScore.ToString(), "Info", 5);

                        if (m_boolVotingSystemEnabled && !m_boolVotingStarted)
                        {

                            int iSecondsTillVoteStart = timeToEnd - this.m_iVotingDuration;
                            m_timeVoteStart = DateTime.Now.AddSeconds(iSecondsTillVoteStart);

                            if (iSecondsTillVoteStart > 0)
                            {
                                m_tsStartVote = TimeSpan.FromSeconds(iSecondsTillVoteStart);
                                WritePluginConsole("Estimated voting start in " + ToReadableString(m_tsStartVote) + " @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                            }
                            else
                            {
                                WritePluginConsole("Estimated voting start " + (-iSecondsTillVoteStart).ToString() + " seconds ago, @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                            }
                        }

                        if (m_boolVotingSystemEnabled)
                        {
                            m_timeVoteEnd = DateTime.Now.AddSeconds(timeToEnd + this.m_iStopVoteTime - this.m_iEndTimeLeeway);
                        }

                        if (m_boolVotingStarted)
                        {
                            TimeSpan tempSpan = DateTime.Now - this.m_timeVoteStart;
                            WritePluginConsole("Voting duration: " + ToReadableString(tempSpan) + ".", "Info", 3);
                        }
                    }

                    m_timePrevious = DateTime.Now;

                    for (int i = 0; i < m_iPrevTicket.Length; i++)
                    {
                        m_iPrevTicket[i] = m_listCurrTeamScore[i].Score;
                    }

                }
                else if (m_strCurrentGameMode.Contains("Rush"))
                {
                    //if (m_iPrevTicket != null && m_iPrevTicket[0] != -1 && m_listCurrTeamScore != null && m_listCurrTeamScore.Count > 0)
                    //{
                    //    if (maxTickets < m_listCurrTeamScore[0].Score)
                    //    {
                    //        maxTickets = m_listCurrTeamScore[0].Score;

                    //        if (m_listCurrTeamScore[0].Score > m_iPrevTicket[0] + 5 && m_listCurrTeamScore[0].Score + 10 > maxTickets)
                    //        {
                    //            currentMcom++;
                    //        }
                            
                    //    }
                    //    WritePluginConsole("Current Mcom is: " + currentMcom.ToString() + ". Ticket count: " + m_listCurrTeamScore[0].Score, "Info", 2);
                    //}

                    

                    //m_timePrevious = DateTime.Now;
                    //m_iPrevTicket[0] = m_listCurrTeamScore[0].Score;

                    if (m_boolVotingSystemEnabled && !m_boolVotingStarted)
                    {
                        m_timeVoteStart = DateTime.Now.AddSeconds(m_iRushVoteStartTime - m_iCurrentRoundTime);
                        m_timeVoteEnd = m_timeVoteStart.AddSeconds(m_iVotingDuration);

                        if (DateTime.Now < m_timeVoteStart)
                        {
                            WritePluginConsole("Voting will start in " + ToReadableString(this.m_timeVoteStart - DateTime.Now) + " @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                        else
                        {
                            WritePluginConsole("Voting should have started " + ToReadableString(DateTime.Now - this.m_timeVoteStart) + " ago, @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                    }
                }
                else
                {
                    WritePluginConsole("Current gamemode is not recognised: " + m_strCurrentGameMode, "Info", 1);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("EXCEPTION CAUGHT IN: SetVoteStartAndEndTimes", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        private void ProcessChatMessage(string speaker, string message)
        {
            CPrivileges cpPlayerPrivs = this.GetAccountPrivileges(speaker);

            // --------------
            // VOTE
            // --------------
            Match match = Regex.Match(message, @"^/(\d)");
            if (match.Success && message.Length == 2)
            {
                if (m_boolVotingStarted && !m_dictVoting.ContainsKey(speaker))
                {
                    int vote = Convert.ToInt32(match.Groups[1].Value);

                    if (vote > 0 && vote <= m_listMapOptions.Count)
                    {
                        m_dictVoting.Add(speaker, vote - 1);
                        bool success = m_players.SetVote(speaker, vote - 1);

                        if (!success)
                        {
                            WritePluginConsole(speaker + " was not found in player list. Vote not counted.", "Error", 1);
                        }

                        string mapandmode = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]).PublicLevelName;
                        if (m_enumShowGamemode == enumBoolYesNo.Yes)
                        {
                            mapandmode += " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[m_dictVoting[speaker]]);
                        }

                        this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You voted for " + mapandmode, "player", speaker);
                        WritePluginConsole("^7" + speaker + "^0 voted for ^6" + GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[m_dictVoting[speaker]]), "Info", 2);
                    }
                    else
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Your vote was not recognised. Please try again.", "player", speaker);
                    }
                }
                else if (m_boolVotingStarted && m_dictVoting.ContainsKey(speaker))
                {
                    int vote = Convert.ToInt32(match.Groups[1].Value);
                    if (vote > 0 && vote <= m_listMapOptions.Count)
                    {
                        if (m_dictVoting[speaker] != vote - 1)
                        {
                            string mapandmode0 = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]).PublicLevelName;
                            string mapandmode1 = GetMapByFilename(m_listMapOptions[vote - 1]).PublicLevelName;
                            if (m_enumShowGamemode == enumBoolYesNo.Yes)
                            {
                                mapandmode0 += " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[m_dictVoting[speaker]]);
                                mapandmode1 += " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[vote - 1]);
                            }

                            this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You changed your vote from " + mapandmode0 + " to " + mapandmode1, "player", speaker);
                            WritePluginConsole("^7" + speaker + "^0 changed their vote from ^6" + GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[m_dictVoting[speaker]]) + "^0 to ^6" + GetMapByFilename(m_listMapOptions[vote - 1]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[vote - 1]), "Info", 2);

                            m_dictVoting[speaker] = vote - 1;
                        }
                        else
                        {
                            string mapandmode = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]).PublicLevelName;
                            if (m_enumShowGamemode == enumBoolYesNo.Yes)
                            {
                                mapandmode += " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[m_dictVoting[speaker]]);
                            }
                            this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You have already voted for " + mapandmode, "player", speaker);
                        }
                    }
                    else
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Your vote was not recognised. Please try again.", "player", speaker);
                    }
                }
                else if (m_boolVotingSystemEnabled)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": There is no vote currently in progress. Voting will start in about " + ToReadableString(m_tsStartVote), "player", speaker);
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Voting has ended, the next map will be " + m_strNextMap, "player", speaker);
                }
            }

            // --------------
            // VOTING OPTIONS
            // --------------
            match = Regex.Match(message, @"^/v");
            if (match.Success && message.Length == 2)
            {
                if (m_boolVotingStarted)
                {
                    DisplayVoteOptions(speaker);
                    WritePluginConsole(speaker + " requested the map options", "Info", 3);
                }
                else if (m_boolVotingSystemEnabled)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": There is no vote currently in progress. Voting will start in about " + ToReadableString(m_tsStartVote), "player", speaker);
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Voting has ended, the next map will be " + m_strNextMap, "player", speaker);
                }
            }

            // --------------
            // NEXT MAP
            // --------------
            match = Regex.Match(message, @"^/next ?map");
            if (match.Success)
            {
                WritePluginConsole(speaker + " requested the next map", "Info", 3);
                DisplayNextMap(speaker);
            }

            // --------------
            // CALL VOTEMAP
            // --------------
            match = Regex.Match(message, @"^/vote ?map");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 started a Votemap", "Info", 3);
                if (!m_boolVotingSystemEnabled)
                {
                    StartVotingSystem();
                }
                StartVotingPoll();
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to call a Votemap.", "player", speaker);
            }

            // --------------
            // ENABLE/DISABLE VOTE MAP
            // --------------
            match = Regex.Match(message, @"enablevotemap");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 enabled xVotemap", "Info", 2);
                this.ExecuteCommand("procon.protected.send", "admin.say", "xVotemap ENABLED!", "player", speaker);

                this.m_boolVotemapEnabled = true;            
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to enable xVotemap.", "player", speaker);
            }

            match = Regex.Match(message, @"disablevotemap");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 diasbled xVotemap", "Info", 2);
                this.ExecuteCommand("procon.protected.send", "admin.say", "xVotemap DISABLED!", "player", speaker);

                this.m_boolVotemapEnabled = false;
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to disable xVotemap.", "player", speaker);
            }

            // --------------
            // INCORRECT VOTE ATTEMPTS
            // --------------
            match = Regex.Match(message, @"^\d");
            if (match.Success && message.Length == 1 && m_boolVotingStarted && !m_dictVoting.ContainsKey(speaker))
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": If you are trying to vote, you seem to have forgotten the / in front of the map number.", "player", speaker);
            }
        }      

        private void StartVotingPoll(){
            try
            {
                WritePluginConsole("Voting poll: Starting...", "Info", 4);
                m_boolVotingStarted = true;
                m_timeVoteStart = DateTime.Now;
                m_dictVoting = new Dictionary<string, int>();

                GenerateMapOptions();

                if (m_boolVotingStarted)
                {
                    if (m_strEnableVoteBanner == "First")
                    {
                        DisplayVoteBanner();
                    }
                    else if (m_strEnableVoteBanner == "All")
                    {
                        this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayVoteBanner", "0", m_iVotingOptionsInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayVoteBanner");
                    }

                    GenerateVoteOptionsDisplay();

                    this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayVoteOptions", "6", m_iVotingOptionsInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayVoteOptions", "all");
                }
                WritePluginConsole("^bVoting poll: Started!", "Info", 3);
            }
            catch (Exception e)
            {
                WritePluginConsole("EXCEPTION CAUGHT IN: StartVotingPoll", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void DisplayVoteBanner()
        {
            List<Players.Squad> squads = m_players.GetNonvotedSquads();
            string temp = "";
            foreach (Players.Squad s in squads)
            {
                temp += "(" + s.TeamID + "," + s.SquadID + ") ";
            }
            WritePluginConsole("Nonvoted squads:" + temp, "Info", 4);

            if (this.m_strBannerType == "Yell" || this.m_strBannerType == "Both")
            {
                this.ExecuteCommand("procon.protected.send", "admin.yell", "Vote Next Map Now!", m_iBannerYellDuration.ToString(), "all");
            }

            if (this.m_strBannerType == "Chat" || this.m_strBannerType == "Both")
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%       VOTE NEXT MAP       %%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%       VOTE NEXT MAP       %%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
                this.ExecuteCommand("procon.protected.send", "admin.say", "%%%%%%%%%%%%%%%%%%%%%%%", "all");
            }

            WritePluginConsole("Displayed vote banner", "Work", 3);
        }
        
        private void GenerateMapOptions()
        {
            try
            {
                WritePluginConsole("Generate map options: Starting...", "Info", 4);
                m_listMapOptions = new List<string>();
                m_listGamemodeOptions = new List<string>();
                List<MaplistEntry> tempMaplist = new List<MaplistEntry>(m_listCurrMapList);
                string mapnames = "";

                try
                {
                    for (int i = 0; i < tempMaplist.Count; i++)
                    {
                        if (tempMaplist[i].MapFileName == m_strCurrentMap && m_enumExcludeCurrMap == enumBoolYesNo.Yes)
                        {                           
                            WritePluginConsole("^6" + GetMapByFilename(tempMaplist[i].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(tempMaplist[i].Gamemode) + "^0 was removed from maplistoptions.", "Info", 3);
                            tempMaplist.RemoveAt(i);
                        }
                    }
                }
                catch (Exception e)
                {
                    WritePluginConsole("Exception Caught while trying to remove current map, continuing without removing =\\", "Error", 1);
                    WritePluginConsole(e.Message, "Error", 1);
                }


                tempMaplist = SortMapList(tempMaplist);


                int iNumOfOptions = Math.Min(m_iNumOfMapOptions, tempMaplist.Count);

                if (iNumOfOptions > 1)
                {
                    List<int> selection = new List<int>();
                    try
                    {
                        selection = PseudoRandom(iNumOfOptions, tempMaplist.Count, m_iRandomness);
                    }
                    catch (Exception e)
                    {
                        WritePluginConsole("EXCEPTION CAUGHT IN: PseudoRandom", "Error", 1);
                        WritePluginConsole(e.Message, "Error", 1);
                        throw;
                    }

                    // sort by map name length
                    selection.Sort(delegate(int map1, int map2)
                    {
                        return GetMapByFilename(tempMaplist[map2].MapFileName).PublicLevelName.Length.CompareTo(GetMapByFilename(tempMaplist[map1].MapFileName).PublicLevelName.Length);
                    });

                    // put longest names with shortest
                    List<int> newSelection = new List<int>();
                    int k = 0;
                    while (selection.Count > 0)
                    {
                        if (k % 2 == 0)
                        {
                            newSelection.Add(selection[0]);
                            selection.RemoveAt(0);
                        }
                        else
                        {
                            newSelection.Add(selection[selection.Count - 1]);
                            selection.RemoveAt(selection.Count - 1);
                        }
                        k++;
                    }

                    for (int i = 0; i < newSelection.Count; i++)
                    {
                        m_listMapOptions.Add(tempMaplist[newSelection[i]].MapFileName);
                        m_listGamemodeOptions.Add(tempMaplist[newSelection[i]].Gamemode);

                        mapnames += "^6" + GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[i]) + "^0 | ";
                    }

                    WritePluginConsole("^bVotemap Options: " + mapnames.Substring(0, mapnames.Length - 3), "Info", 2);
                }
                else
                {
                    WritePluginConsole("Not enough maps in the maplist to start a vote. Stopping voting system", "Info", 2);
                    StopVotingSystem();
                }
                WritePluginConsole("GenerateMapOptions: Done!", "Info", 4);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in GenerateMapOptions", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        private List<MaplistEntry> SortMapList(List<MaplistEntry> origMaplist)
        {
            try
            {
                WritePluginConsole("SortMapList: Starting...", "Info", 4);
                WritePluginConsole("Pastmaplist count:" + m_listPastMaps.Count, "Info", 4);
                WritePluginConsole("Currmaplist count:" + origMaplist.Count, "Info", 4);
                for (int i = 0; i < m_listPastMaps.Count; i++)
                {
                    for (int j = 0; j < origMaplist.Count; j++)
                    {
                        if (mapSortSettings == "Map Only")
                        {
                            if (m_listPastMaps[i].MapFileName == origMaplist[j].MapFileName)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                            }
                        }
                        else if (mapSortSettings == "Map and Gametype")
                        {
                            if (m_listPastMaps[i].MapFileName == origMaplist[j].MapFileName && m_listPastMaps[i].Gamemode == origMaplist[j].Gamemode)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                                break;
                            }
                        }
                        else if (mapSortSettings == "Gametype Only")
                        {
                            if (m_listPastMaps[i].Gamemode == origMaplist[j].Gamemode)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                            }
                        }
                    }
                }

                for (int j = 0; j < origMaplist.Count; j++)
                {
                    WritePluginConsole("Sorted: " + j.ToString() + " ^6" + GetMapByFilename(origMaplist[j].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(origMaplist[j].Gamemode), "Info", 4);
                }

                WritePluginConsole("SortMapList: Done!", "Info", 4);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in SortMapList, continuing without proper map order =\\", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

            return origMaplist;
        }    

        private void GenerateVoteOptionsDisplay()
        {
            m_listOptsDisplay = new List<string>();
            m_listOptsDisplay.Add("Type: /1 , /2 , ... in squad chat to vote for the next map!");

            string padding = "";
            string strNamenMode = "";
            string strLine = "";
            int iChatMiddle = 15;

            for (int i = 0; i < m_listGamemodeOptions.Count; i++)
            {
                if (i % 2 == 0)
                {
                    if (m_enumShowGamemode == enumBoolYesNo.Yes)
                    {
                        strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[i]);
                        padding = GeneratePadding(strNamenMode, iChatMiddle);
                    }
                    else
                    {
                        strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName;
                        padding = GeneratePadding(GetMapByFilename(m_listMapOptions[i]).PublicLevelName, iChatMiddle);
                    }

                    strLine = " /" + (i + 1).ToString() + "  " + strNamenMode + padding;
                }
                else
                {
                    if (m_enumShowGamemode == enumBoolYesNo.Yes)
                    {
                        strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[i]);
                    }
                    else
                    {
                        strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName;
                    }

                    strLine += "|  /" + (i + 1).ToString() + "  " + strNamenMode;

                    m_listOptsDisplay.Add(strLine);
                    strLine = "";
                }

                if (i == m_listGamemodeOptions.Count - 1 && i % 2 == 0)
                {
                    m_listOptsDisplay.Add(strLine);
                    strLine = "";
                }
            }
        }

        public void DisplayVoteOptions(string target){              
            foreach (string s in m_listOptsDisplay)
            {
                if (target == "all")
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", s, "all");
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", s, "player", target);
                }
            }
            WritePluginConsole("Displayed voting options", "Work", 4);
        }

        private void DisplayVoteResults()
        {
            int[] votes = new int[m_listMapOptions.Count];

            foreach (KeyValuePair<string, int> kv in m_dictVoting)
            {
                votes[kv.Value]++;
            }

            int winner = MaxIndex(votes);
            double winningPercent = 0;
            if (m_dictVoting.Count != 0)
            {
                winningPercent = Convert.ToDouble(votes[winner]) / Convert.ToDouble(m_dictVoting.Count);
            }

            if (m_iCurrPlayerCount > 0)
            {
                double votingTurnout = Convert.ToDouble(m_dictVoting.Count) / Convert.ToDouble(m_iCurrPlayerCount);
                WritePluginConsole("^bVoting Ended. " + votingTurnout.ToString("##0%") + " of the players voted.", "Info", 2);
            }

            for (int i = 0; i < m_listMapOptions.Count; i++)
            {
                WritePluginConsole("^bVotes: ^6" + GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[i]) + "^0: " + votes[i], "Info", 2);
            }

            this.ExecuteCommand("procon.protected.send", "admin.say", "VOTING ENDED!", "all");
           
            if (m_dictVoting.Count >= m_iVoteThres)
            {
                if (disableVoteResults == enumBoolYesNo.No)
                {
                    string mapandmode = GetMapByFilename(m_listMapOptions[winner]).PublicLevelName;
                    if (m_enumShowGamemode == enumBoolYesNo.Yes)
                    {
                        mapandmode += " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[winner]);
                    }
                    this.ExecuteCommand("procon.protected.send", "admin.say", mapandmode + " Won with " + winningPercent.ToString("###%") + " of the votes (" + votes[winner] + "/" + m_dictVoting.Count + ")", "all");
                }
                WritePluginConsole("^b^6" + GetMapByFilename(m_listMapOptions[winner]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[winner]) + "^0 Won", "Info", 2);


                m_strNextMap = GetMapByFilename(m_listMapOptions[winner]).PublicLevelName;
                m_strNextMode = ConvertGamemodeToShorthand(m_listGamemodeOptions[winner]);
                if (m_iNextMapDisplayInterval > 0)
                {
                    this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayNextMap", "90", m_iNextMapDisplayInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayNextMap", "all");
                }

                SetMap(m_listMapOptions[winner], m_listGamemodeOptions[winner]);

                // send infomation to UMM just in case maplist changes at EOR
                this.ExecuteCommand("procon.protected.plugins.call", "CUltimateMapManager", "VotedMapInfo", m_listMapOptions[winner], m_listGamemodeOptions[winner]); 
            }
            else
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", "Votemap failed. The total number votes (" + m_dictVoting.Count + ") did not exceed the threshold (" + m_iVoteThres + ")", "all");
                WritePluginConsole("^bVotemap failed^n. The total number votes (" + m_dictVoting.Count + ") did not exceed the threshold (" + m_iVoteThres + "), next map will not be changed.", "Info", 2);
            }
        }

        public void DisplayNextMap(string target){

            string mapandmode = m_strNextMap;
            if (m_enumShowGamemode == enumBoolYesNo.Yes)
            {
                mapandmode += " " + m_strNextMode;
            }

            if (target == "all")
            {
                WritePluginConsole("Displayed next map", "Work", 3);
                this.ExecuteCommand("procon.protected.send", "admin.say", "Next Map: " + mapandmode, "all");
            }
            else
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", "Next Map: " + mapandmode, "player", target);
            }
        }

        #endregion

        #region Tools

        private void SetMap(string mapName, string gamemode)
        {
            int index = 0;
            for (int i = 0; i < m_listCurrMapList.Count; i++)
            {
                if (m_listCurrMapList[i].MapFileName.CompareTo(mapName) == 0 && m_listCurrMapList[i].Gamemode.CompareTo(gamemode) == 0)
                {
                    index = i;
                    WritePluginConsole("Map found in current maplist: " + index, "Info", 5);
                    break;
                }
            }
            this.ExecuteCommand("procon.protected.send", "mapList.setNextMapIndex", index.ToString());

            WritePluginConsole("Next map index set to: " + index, "Work", 5);
        }

        private string ConvertGamemodeToShorthand(string gamemode)
        {
            if (gamemode.Contains("Conquest"))
            {
                return "[CQ]";
            }
            else if (gamemode.Contains("RushLarge"))
            {
                return "[R]";
            }
            else if (gamemode.Contains("SquadRush"))
            {
                return "[SR]";
            }
            else if (gamemode.Contains("TeamDeathMatch"))
            {
                return "[TDM]";
            }
            else if (gamemode.Contains("SquadDeathMatch"))
            {
                return "[SDM]";
            }
            return "";
        }

        private void WritePluginConsole(string message, string tag, int level)
        {
            if (tag == "Error")
            {
                tag = "^8" + tag;
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
            
            if (this.m_iDebugLevel >= level)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", line);
            }
        }

        Random random = new Random(DateTime.Now.Millisecond + DateTime.Now.DayOfYear);
        private int RandomNumber(int min, int max)
        {         
            return random.Next(min, max);
        }

        private List<int> PseudoRandom(int mid, int max, int randomness)
        {
            WritePluginConsole("PseudoRandom: Starting...", "Info", 4);
            List<int> selection = new List<int>();

            if (mid == max)
            {
                for (int i = 0; i < mid; i++)
                {
                    selection.Add(i);                   
                }
                WritePluginConsole("No flexiblity in displayed map options", "Info", 3);
            }
            else if (mid < max)
            {            
                double[] rdmTable = RandomTable(mid, max, randomness);
                int rnd = 0;
                for (int i = 0; i < mid; i++)
                {
                    for (int m = 0; m < 100; m++) // attempt to find an unadded map
                    {
                        int testValue = -1;
                        rnd = RandomNumber(0, 1000);
                        
                        testValue = FindInTable(rnd, rdmTable);
                        WritePluginConsole("Rnd: " + rnd + "result: " + testValue, "Info", 5);

                        if (testValue != -1 && !selection.Contains(testValue))
                        {
                            WritePluginConsole("Useable value found: " + testValue, "Info", 4);
                            selection.Add(testValue);
                            break;
                        }
                        else if (selection.Contains(testValue))
                        {
                           // WritePluginConsole("Duplicate found", "Info", 2);
                        }

                       // WritePluginConsole("Looping...", "Info", 2);
                    }
                }
            }

            WritePluginConsole("PseudoRandom: Done!", "Info", 4);

            return selection;
        }

        private int FindInTable(int value, double[] table)
        {
            int result = -1;
            for (int i = 0; i < table.Length; i++)
            {
                if (value < table[i])
                {
                    result = i;
                    break;
                }
            }
            return result;
        }

        private double[] RandomTable(int mid, int max, int randomness)
        {
            WritePluginConsole("RandomTable: Starting...", "Info", 4);
            double[] table = new double[max];

            double[] prob = new double[max];
            double probSum = 0;

            if (randomness == 0)
            {
                WritePluginConsole("RTable: If 1", "Info", 5);

                double step = 1000 / mid;
                for (int i = 0; i < max; i++)
                {
                    if (i < mid)
                    {
                        prob[i] = max;
                        probSum += prob[i];
                    }
                }
            }
            else if (randomness > 0 && randomness < 5)
            {
                WritePluginConsole("RTable: If 2", "Info", 5);

                double factor = (5 - (double)randomness) / 5;  // from 1.0 to 0.0
                
                double xStart = factor * mid;
                double xStop = mid + (1 - factor) * (max-mid);              
                double diff = xStop - xStart;
                double grad = max / diff;

                double tri = diff * max * 0.5;

                for (int i = 0; i < max; i++)
                {
                    if (i + 1 < xStart)
                    {
                        prob[i] = max;

                        WritePluginConsole("RTable: Prenorm Block:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                    }
                    else if (i > xStop)
                    {
                        break;  // do nothing
                    }
                    else
                    {
                        if (i < xStart)
                        {
                            prob[i] = (xStart - i) * max;
                            if (i + 1 > xStop)
                            {
                                prob[i] += tri;

                                WritePluginConsole("RTable: Prenorm End:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                            }
                            else
                            {
                                prob[i] += tri - ((xStop - (i + 1)) * (xStop - (i + 1)) * grad * 0.5);

                                WritePluginConsole("RTable: Prenorm Stub:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                            }
                        }
                        else if (xStart < i && xStop < i + 1)
                        {
                            prob[i] += (xStop - i) * (xStop - i) * grad * 0.5;
                            

                            WritePluginConsole("RTable: Prenorm Tip:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                        }
                        else
                        {
                            prob[i] += (xStop - i) * (xStop - i) * grad * 0.5 - (xStop - (i + 1)) * (xStop - (i + 1)) * grad * 0.5;

                            WritePluginConsole("RTable: Prenorm Chunk:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                        }
                    }
                    probSum += prob[i];
                }
            }
            else if (randomness >= 5 && randomness <= 10)
            {
                WritePluginConsole("RTable: If 3", "Info", 5);

                double factor = (10 - (double)randomness) / 5;  // from 1.0 to 0.0
                double smlTri = 0.5 * factor;
                double baseSqr = max * (1 - factor);

                WritePluginConsole("RTable: Tri:" + string.Format("{0:0.00}", smlTri) + "base:" + string.Format("{0:0.0}", baseSqr) + "factor:" + string.Format("{0:0.0}", factor), "Info", 5);

                for (int i = 0; i < max; i++)
                {
                    prob[i] = (max * 2 - i * 2 - 1) * smlTri + baseSqr;
                    probSum += prob[i];
                    WritePluginConsole("RTable: Prenorm:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                }
            }
            // Normalise
            for (int i = 0; i < max; i++)
            {
                prob[i] = prob[i] * 1000 / probSum;
                WritePluginConsole("Map " + i.ToString() + " Probability: " + string.Format("{0:0.0}%", prob[i]/10), "Info", 4);
            }

            table[0] = prob[0];
            WritePluginConsole("RTable Sum: " + 0.ToString() + ": " + string.Format("{0:0.0}", table[0]), "Info", 5);
            for (int i = 1; i < max; i++)
            {
                table[i] = table[i - 1] + prob[i];
                WritePluginConsole("RTable Sum: " + i.ToString() + ": " + string.Format("{0:0.0}", table[i]), "Info", 5);
            }

            WritePluginConsole("RandomTable: Done!", "Info", 4);
            return table;
        }

        public int MaxIndex(int[] array)
        {
            int maxIndex = -1;
            int maxValue = -1;
            int index = 0;
            List<int> draws = new List<int>();

            foreach (int value in array)
            {
                if (value.CompareTo(maxValue) > 0)
                {
                    maxIndex = index;
                    maxValue = value;

                    draws = new List<int>();
                    draws.Add(index);
                }
                else if (value.CompareTo(maxValue) == 0)
                {
                    draws.Add(index);
                }
                index++;
            }

            if (draws.Count > 1)
            {
                maxIndex = draws[RandomNumber(0, draws.Count)];
                WritePluginConsole("Winning vote tie, ^6" + GetMapByFilename(m_listMapOptions[maxIndex]).PublicLevelName + " " + ConvertGamemodeToShorthand(m_listGamemodeOptions[maxIndex]) + "^0 randomly selected as winner.", "Info", 3);
            }

            return maxIndex;
        }

        public double GetMin(double[] DoubleCollection)
        {
            double min = double.MaxValue;
            foreach (double i in DoubleCollection)
            {
                if (i < min)
                {
                    min = i;
                }
            }
            return min;
        }

        public bool ListsEqual(List<MaplistEntry> a, List<MaplistEntry> b)
        {
            if (a.Count != b.Count)
            {
                WritePluginConsole("Maplist counts not equal: " + a.Count + " != " + b.Count, "Info", 5);
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].MapFileName != b[i].MapFileName || a[i].Gamemode != b[i].Gamemode || a[i].Index != b[i].Index || a[i].Rounds != b[i].Rounds)
                {
                    WritePluginConsole("Maps not equal @ " + i + " A " + a[i].MapFileName + " " + a[i].Gamemode + " " + a[i].Rounds, "Info", 5);
                    WritePluginConsole("Maps not equal @ " + i + " B " + b[i].MapFileName + " " + b[i].Gamemode + " " + b[i].Rounds, "Info", 5);
                    return false;
                }
            }

            return true;
        }

        public string ToReadableString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Days > 0 ? string.Format("{0:0} days, ", span.Days) : string.Empty,
                span.Hours > 0 ? string.Format("{0:0} hours, ", span.Hours) : string.Empty,
                span.Minutes > 0 ? string.Format("{0:0} minutes, ", span.Minutes) : string.Empty,
                span.Seconds > 0 ? string.Format("{0:0} seconds", span.Seconds) : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            return formatted;
        }

        //private int MeasureText(string text)
        //{
        //    System.Drawing.Font arialBold = new System.Drawing.Font("Tahoma", 12.0F);
        //    System.Drawing.Size textSize = TextRenderer.MeasureText(text, arialBold);
        //    return textSize.Width;
        //}

        private string GeneratePadding(string text, double length)
        {
            string padding = "  ";
            double txtLength = 0.0;

            char[] chars = text.ToCharArray();

            foreach (char c in chars)
            {
                double value = 1.0;
                if (m_dictLetterSizes.TryGetValue(c, out value))
                {
                    txtLength += value;
                }
                else
                {
                    txtLength += 1.0;
                }
            }

            double whitespaceLength = length - txtLength;
            double widthofspace = 0;
            m_dictLetterSizes.TryGetValue(' ', out widthofspace);
            double numOfSpaces = whitespaceLength / widthofspace;
            int iSpaces = (int)Math.Round(numOfSpaces);
            if (iSpaces > 0)
            {
                padding = new String(' ', iSpaces);
            }

            return padding;
        }

        #endregion
    }
}