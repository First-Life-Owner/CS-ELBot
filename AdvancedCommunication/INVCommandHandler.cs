// Eternal Lands Bot
// Copyright (C) 2006  Artem Makhutov
// artem@makhutov.org
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

using System;

namespace cs_elbot.AdvancedCommunication
{
	/// <summary>
	/// description of INVCommandHandler.
	/// </summary>
	public class INVCommandHandler
	{
		private TCPWrapper TheTCPWrapper;
		private BasicCommunication.MessageParser TheMessageParser;
		private MySqlManager TheMySqlManager;
		//private bool CommandIsDisabled;
		private HelpCommandHandler TheHelpCommandHandler;
		private Inventory TheInventory;
        private TradeHandler TheTradeHandler;
        private Stats TheStats;
		
		public INVCommandHandler(TCPWrapper MyTCPWrapper, BasicCommunication.MessageParser MyMessageParser,HelpCommandHandler MyHelpCommandHandler, MySqlManager MyMySqlManager, Inventory MyInventory, TradeHandler MyTradeHandler, Stats MyStats)
		{
			this.TheTCPWrapper = MyTCPWrapper;
			this.TheMessageParser = MyMessageParser;
			this.TheHelpCommandHandler = MyHelpCommandHandler;
			this.TheMySqlManager = MyMySqlManager;
			this.TheInventory = MyInventory;
            this.TheTradeHandler = MyTradeHandler;
            this.TheStats = MyStats;

			//this.CommandIsDisabled = MyMySqlManager.CheckIfCommandIsDisabled("#inv",Settings.botid);
			
			//if (CommandIsDisabled == false)
			{
                if (Settings.IsTradeBot == true)
                {
                    TheHelpCommandHandler.AddCommand("#inventory / #inv / #i - show what I am selling");
                    TheHelpCommandHandler.AddCommand("#invmembers / #im - member rates");
                    TheHelpCommandHandler.AddCommand("#inv - null");
                    TheHelpCommandHandler.AddCommand("#i - null");
                    TheHelpCommandHandler.AddCommand("#inb - null");
                    TheHelpCommandHandler.AddCommand("#sell - null");
                    TheHelpCommandHandler.AddCommand("#selling - null");
                    TheHelpCommandHandler.AddCommand("#im - null");
                    TheHelpCommandHandler.AddCommand("#invmember - null");
                    TheHelpCommandHandler.AddCommand("#invmemver - null");
                    TheHelpCommandHandler.AddCommand("#invmemvers - null");
                }
                TheMessageParser.Got_PM += new BasicCommunication.MessageParser.Got_PM_EventHandler(OnGotPM);
			}
		}

        private void OnGotPM(object sender, BasicCommunication.MessageParser.Got_PM_EventArgs e)
        {
            int UsedSlots = 0;
            string Message = e.Message.ToLower().Replace("\'", "\\\'").Replace("\"", "\\\"");
            string[] Inv=new string[64];
            int maxlen = 4;

            if (Message[0] != '#')
            {
                Message = "#" + Message;
            }

            string[] CommandArray = Message.Split(' ');
            bool isinv = false,isinvmembers = false;
            if (CommandArray[0] == "#inv" || CommandArray[0] == "#i" || CommandArray[0] == "#inb" || CommandArray[0] == "#sell" || CommandArray[0] == "#selling" || CommandArray[0] == "#inventory")
                isinv = true;
            if (CommandArray[0] == "#invmembers" || CommandArray[0] == "#im" || CommandArray[0] == "#invmemvers" || CommandArray[0] == "#invmember" || CommandArray[0] == "#invmemver")
                isinvmembers = true;
            if (isinv || isinvmembers)
            {
                bool disabled = TheMySqlManager.CheckIfCommandIsDisabled("#inv", Settings.botid);

                string str1 = "", str2 = "";

                if (TheInventory.GettingInventoryItems == true)
                {
                    str2 = "I am building my inventory list, please try again in a few seconds";
                    str1 = str1.PadRight(str2.Length, '=');
                    str1 = "[" + str1;
                    str2 = "[" + str2;
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str1));
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str2));
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str1));

                    return;
                }

                if (disabled == true)
                {
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "This command is disabled"));
                    return;
                }

                if (Settings.IsTradeBot == false)
                {
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "Sorry, I am not a trade bot!"));
                    return;
                }
                int rank = TheMySqlManager.GetUserRank(e.username, Settings.botid);
                if (rank < TheMySqlManager.GetCommandRank("#inv", Settings.botid))
                {
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "You are not authorized to use this command!"));
                    return;
                }

                if ((CommandArray[0] == "#im" || CommandArray[0] == "#invmembers" || CommandArray[0] == "#invmemvers" || CommandArray[0] == "#invmember" || CommandArray[0] == "#invmemver") && ((rank < TheMySqlManager.GetCommandRank("#invmembers", Settings.botid)) && TheMySqlManager.CheckIfBannedGuild( e.username, Settings.botid) < 1))
                {
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "You are not authorized to use this command!"));
                    return;
                }

                if (this.TheTradeHandler.AmITrading() && e.username != TradeHandler.username)
                {
                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "I am currently trading, please retry shortly."));
                    return;
                }

                if (CommandArray.Length < 1)
                    goto WrongArguments;

                char invFiller = TheMySqlManager.getInvFiller();

                System.Collections.ArrayList MyInventoryList = TheInventory.GetInventoryList();
                System.Collections.Hashtable MySellingItemsList = TheMySqlManager.GetSellingItemList(0);

                // sum up all inventory items if the items are on sale
                System.Collections.SortedList TheInventoryList = new System.Collections.SortedList();

                foreach (Inventory.inventory_item MyInventoryItem in MyInventoryList)
                {
                    //adjust the number of slots used...
                    if (MyInventoryItem.pos < 36)
                    {
                        if (MyInventoryItem.is_stackable)
                            UsedSlots++;
                        else
                            UsedSlots = UsedSlots + (int)(MyInventoryItem.quantity);
                    }
                    //adjust the amount

                    //only display if in selling list
                    if (MySellingItemsList.Contains(MyInventoryItem.SqlID))
                    {
                        //if already in inventory output (eg.., two slots) just sum the quantity
                        //otherwise add to inventory output
                        if (TheInventoryList.Contains(MyInventoryItem.SqlID) && MyInventoryItem.pos < 36)
                        {
                            Inventory.inventory_item TempInventoryItem = (Inventory.inventory_item)TheInventoryList[MyInventoryItem.SqlID];
                            TempInventoryItem.quantity += MyInventoryItem.quantity;
                            TheInventoryList[MyInventoryItem.SqlID] = TempInventoryItem;
                        }
                        else
                        {
                            if (MyInventoryItem.pos < 36)
                            {

                                TheInventoryList.Add(MyInventoryItem.SqlID, MyInventoryItem);
                            }
                        }
                    }
                }

                //foreach (Inventory.inventory_item MyInventoryItem in TheInventoryList.Values)
                //{
                //    if(maxlen<MyInventoryItem.name.Length && (MyInventoryItem.pos < 36))
                //        maxlen = MyInventoryItem.name.Length;
                //}
                //maxlen++;
                maxlen = 20 + 25 - Settings.Loginname.Length;
                // pm all summed up inventory items on sale                
                int i = 0;
                for (i = 0; i < 64; i++)
                    Inv[i] = "ZZZZZZZZZZZZZZZZ";
                int c = 0;
                string str = "";
                str2 = "";
                str = "[";
                str = str.PadRight(maxlen + 27, '-');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                bool Member = (TheMySqlManager.CheckIfTradeMember(e.username, Settings.botid) == true);

                str = "[name".PadRight(maxlen, ' ');
                str = str + "|qty".PadRight(7, ' ');
                str = str + "|price".PadRight(14, ' ');
                str = str + "|id".PadRight(6, ' ');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));

                str = "[";
                str = str.PadRight(maxlen + 27, '-');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));

                string msg, msg2;                

                foreach (Inventory.inventory_item MyInventoryItem in TheInventoryList.Values)
                {
                    if (MyInventoryItem.pos < 36)
                    {
                        msg = ("[" + MyInventoryItem.name).PadRight(maxlen, i == 0 ? ' ' : invFiller);
                        {
                            uint tempAmount = 0;
                            if ((TheMySqlManager.ReservedAmount(MyInventoryItem.SqlID)) < MyInventoryItem.quantity)
                            {
                                tempAmount = MyInventoryItem.quantity;
                                tempAmount = tempAmount - (TheMySqlManager.ReservedAmount(MyInventoryItem.SqlID));
                            }
                            else
                            {
                                continue;
                            }

                            msg2 = tempAmount.ToString();
                        }
                        msg2 = msg2.PadLeft(6, i == 0 ? ' ' : invFiller);
                        msg = msg + "|" + msg2;
                        TradeHandler.SellingItem MySellingItem = (TradeHandler.SellingItem)(MySellingItemsList[MyInventoryItem.SqlID]);
                        if(isinv)
                            msg2 = MySellingItem.pricesale.ToString();
                        else
                            msg2 = MySellingItem.pricesalemembers.ToString();
                        if (!msg2.Contains("."))
                            msg2 += ".00gc";
                        else if (msg2[msg2.Length - 2] == '.')
                            msg2 += "0gc";
                        else
                            msg2 += "gc";
                        msg2 = msg2.PadLeft(13, i == 0 ? ' ' : invFiller);
                        msg = msg + "|" + msg2;
                        msg2 = MyInventoryItem.SqlID.ToString().PadLeft(5, i == 0 ? ' ' : invFiller);
                        msg = msg + "|" + msg2;
                        Inv[c] = msg;
                        c++;
                        i = 1 - i;
                    }
                }
                int d;
//                for (d = 0; d < c; d++)
//                {
//                    str = Inv[d].Substring(16) + Inv[d].Substring(0, 16);
//                    Inv[d] = str;
//                }
                Array.Sort(Inv);
                i = maxlen + 13;

                string filter = "";
                if (CommandArray.Length > 1)
                {
                    bool firstTime = true;
                    foreach (string filterPart in CommandArray)
                    {
                        if (firstTime)
                        {
                            firstTime = false;
                            continue;
                        }
                        filter += (" " + filterPart);
                    }
                    //filter = Message.Substring(CommandArray[0].Length);
                }
                else
                    filter = "";

                for (d = 0; d < c; d++)
                {
                    str = Inv[d];
                    //27 chars after the name....
                    if (filter == "" || Inv[d].ToLower().Contains(filter.ToLower().Trim()))
                    {
                        string[] outFields = Inv[d].Split('|');
                        string outString = Inv[d];
                        if (outFields[0].Length > maxlen)
                        {
                            outString = outFields[0].Substring(0, maxlen - 3) + "...";
                            outString = outString.PadRight(maxlen, ' ') + "|".PadRight(7, ' ') + "|".PadRight(14, ' ') + "|".PadRight(6, ' ');
                            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, outString));
                            outString = ("[..." + outFields[0].Substring(maxlen - 3).Trim()).PadRight(maxlen, ' ');
                            int count = 0;
                            foreach (string tempString in outFields)
                            {
                                if (count == 0)
                                {
                                    count++;
                                    continue;
                                }
                                outString += "|" + tempString;
                            }
                        }
                        TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, outString));
                    }
                    else
                    {
                        //for (i = 1; i < CommandArray.Length; i++)
                        {
                            //if (Inv[d].ToLower().Contains(CommandArray[i].ToLower().Trim()))
                            Console.WriteLine("inv(d): " + Inv[d] + " filter: " + filter);
                            if (Inv[d].ToLower().Contains(filter.ToLower().Trim()))
                            {
                                string[] outFields = Inv[d].Split('|');
                                string outString = Inv[d];
                                if (outFields[0].Length > maxlen)
                                {
                                    outString = outFields[0].Substring(0, maxlen - 3) + "...";
                                    outString = outString.PadRight(maxlen, ' ') + "|".PadRight(14, ' ') + "|".PadRight(9, ' ');
                                    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, outString));
                                    outString = ("[..." + outFields[0].Substring(maxlen - 3).Trim()).PadRight(maxlen, ' ');
                                    foreach (string tempString in outFields)
                                    {
                                        outString += "|" + tempString;
                                    }
                                }
                                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, outString));
                                i = CommandArray.Length;
                            }
                        }
                    }
                }

                int Phys = TheMySqlManager.GetBotPhysqiue(Settings.botid);
                int Coord = TheMySqlManager.GetBotCoordination(Settings.botid);
                int carry = (Phys + Coord) * 10;
                //int UsedSpace = TheMySqlManager.GetBotUsedSpace(Settings.botid);
                //int UsedSlots = TheMySqlManager.GetBotUsedSlots(Settings.botid);
                int UsedSpace = TheStats.MyCurrentCarryingAmt;
                int FreeSpace = carry - UsedSpace;
                str = "[";
                str = str.PadRight(maxlen + 27, '-');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                str = "[";
                str += FreeSpace.ToString() + " EMU Avail|";
                str += (36-UsedSlots).ToString() + " Open slot(s)";
                if (rank > 89)
                {
                    //str += TheMySqlManager.moneylevel(Settings.botid).ToString() + " gc";
                    str += "|"+TheInventory.GetMoneyAmount().ToString() + " gc";
                }

                str = str.PadRight(maxlen + 27, ' ');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                str = "[";
                if (Member)
                {
                    if ((CommandArray[0] != "#im" && CommandArray[0] != "#invmembers" && CommandArray[0] != "#invmemvers" && CommandArray[0] != "#invmember" && CommandArray[0] != "#invmemver"))
                    {
                        str = str.PadRight(maxlen + 27, '-');
                        TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                        str = "[Use invmembers (im) to see member prices";
                        str = str.PadRight(maxlen + 27, ' ');
                        TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                    }
                    //if (isinv)
                    //{
                    //    str = "[to see member prices use invmembers or im";
                    //    str = str.PadRight(maxlen + 28, ' ');
                    //    TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));
                    //}
                }
                str = "[";
                str = str.PadRight(maxlen + 27, '-');
                TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, str));

                return;
            }
            return;

        WrongArguments:
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[--------------------------------------"));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[Here is the usage of the #inv command:"));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[#inv                                  "));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[--------------------------------------"));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[Example: #inv                         "));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[Example: #inv silver ore              "));
            TheTCPWrapper.Send(CommandCreator.SEND_PM(e.username, "[--------------------------------------"));
            return;
		}
	}
}
