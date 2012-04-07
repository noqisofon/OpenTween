// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
// 
// This file is part of OpenTween.
// 
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
// 
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details. 
// 
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Resources;


namespace OpenTween
{


    public partial class AppendSettingDialog : Form
    {
        private static AppendSettingDialog _instance = new AppendSettingDialog();
        private Twitter twitter_;
        private HttpConnection.ProxyType my_proxy_type_;
        private bool validation_error_ = false;
        private MyCommon.EVENTTYPE my_event_notify_flag_;
        private MyCommon.EVENTTYPE is_my_event_notify_flag_;
        private string my_translated_retweets_;
        public bool hide_duplicated_retweets_;
        public bool is_preview_foursquare_;
        public int foursquare_preview_height_;
        public int foursquare_preview_width_;
        public int foursquare_preview_zoom_;
        public bool is_list_statuses_include_rts_;
        public List<UserAccount> user_accounts_;
        private long initial_user_id_;
        public bool tab_mouse_lock_;
        public bool is_remove_same_event_;
        public bool is_notify_use_growl_;
        public TwitterDataModel.Configuration twitter_config_ = new TwitterDataModel.Configuration();
        private string pin_;

        public class IntervalChangedEventArgs : EventArgs
        {
            public bool UserStream;
            public bool Timeline;
            public bool Reply;
            public bool DirectMessage;
            public bool PublicSearch;
            public bool Lists;
            public bool UserTimeline;
        }


        public delegate void IntervalChangedEventHandler(object sender,IntervalChangedEventArgs e);


        public event IntervalChangedEventHandler IntervalChanged;


        private void TreeViewSetting_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if ( this.TreeViewSetting.SelectedNode == null )
                return;
            Panel panel = (Panel)this.TreeViewSetting.SelectedNode.Tag;
            if ( panel == null )
                return;
            
            panel.Enabled = false;
            panel.Visible = false;
        }


        private void TreeViewSetting_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if ( e.Node == null )
                return;
            Panel panel = (Panel)e.Node.Tag;
            if ( panel == null )
                return;
            panel.Enabled = true;
            panel.Visible = true;

            if ( panel.Name == "PreviewPanel" ) {
                if ( GrowlHelper.IsDllExists ) {
                    IsNotifyUseGrowlCheckBox.Enabled = true;
                } else {
                    IsNotifyUseGrowlCheckBox.Enabled = false;
                }
            }
        }


        private void Save_Click(object sender, EventArgs e)
        {
            if ( MyCommon.IsNetworkAvailable() &&
                (ComboBoxAutoShortUrlFirst.SelectedIndex == (int)MyCommon.UrlConverter.Bitly || ComboBoxAutoShortUrlFirst.SelectedIndex == (int)MyCommon.UrlConverter.Jmp) ) {
                // bit.ly 短縮機能実装のプライバシー問題の暫定対応
                // bit.ly 使用時はログインIDとAPIキーの指定を必須とする
                // 参照: http://sourceforge.jp/projects/opentween/lists/archive/dev/2012-January/000020.html
                if ( string.IsNullOrEmpty( TextBitlyId.Text ) || string.IsNullOrEmpty( TextBitlyPw.Text ) ) {
                    MessageBox.Show( "bit.ly のログイン名とAPIキーの指定は必須項目です。", Application.ProductName );
                    validation_error_ = true;
                    TreeViewSetting.SelectedNode = TreeViewSetting.Nodes ["ConnectionNode"].Nodes ["ShortUrlNode"]; // 動作タブを選択
                    TreeViewSetting.Select();
                    TextBitlyId.Focus();
                    
                    return;
                }

                if ( !BitlyValidation( TextBitlyId.Text, TextBitlyPw.Text ) ) {
                    MessageBox.Show( Properties.Resources.SettingSave_ClickText1 );
                    validation_error_ = true;
                    TreeViewSetting.SelectedNode = TreeViewSetting.Nodes ["ConnectionNode"].Nodes ["ShortUrlNode"]; // 動作タブを選択
                    TreeViewSetting.Select();
                    TextBitlyId.Focus();
                    
                    return;
                } else {
                    validation_error_ = false;
                }
            } else {
                validation_error_ = false;
            }

            this.user_accounts_.Clear();
            foreach ( object that in this.AuthUserCombo.Items ) {
                this.user_accounts_.Add( (UserAccount)that );
            }
            if ( this.AuthUserCombo.SelectedIndex > -1 ) {
                foreach ( UserAccount user_account in this.user_accounts_ ) {
                    if ( user_account.Username.ToLower() == ((UserAccount)this.AuthUserCombo.SelectedItem).Username.ToLower() ) {
                        twitter_.Initialize( user_account.Token, user_account.TokenSecret, user_account.Username, user_account.UserId );
                        if ( user_account.UserId == 0 ) {
                            twitter_.VerifyCredentials();
                            user_account.UserId = twitter_.UserId;
                        }
                        break;
                    }
                }
            } else {
                twitter_.ClearAuthInfo();
                twitter_.Initialize( string.Empty, string.Empty, string.Empty, 0 );
            }

#if UA
            //フォロー
            if (this.FollowCheckBox.Checked)
            {
                //現在の設定内容で通信
                HttpConnection.ProxyType ptype;
                if (RadioProxyNone.Checked)
                {
                    ptype = HttpConnection.ProxyType.None;
                }
                else if (RadioProxyIE.Checked)
                {
                    ptype = HttpConnection.ProxyType.IE;
                }
                else
                {
                    ptype = HttpConnection.ProxyType.Specified;
                }
                string padr = TextProxyAddress.Text.Trim();
                int pport = int.Parse(TextProxyPort.Text.Trim());
                string pusr = TextProxyUser.Text.Trim();
                string ppw = TextProxyPassword.Text.Trim();
                HttpConnection.InitializeConnection(20, ptype, padr, pport, pusr, ppw);

                string ret = twitter_.PostFollowCommand(ApplicationSettings.FeedbackTwitterName);
            }
#endif
            IntervalChangedEventArgs interval_changed_eventargs = new IntervalChangedEventArgs();
            //bool isIntervalChanged = false;
            bool is_interval_changed = false;

            try {
                UserstreamStartup = this.StartupUserstreamCheck.Checked;

                if ( UserstreamPeriodInt != int.Parse( UserstreamPeriod.Text ) ) {
                    UserstreamPeriodInt = int.Parse( UserstreamPeriod.Text );
                    interval_changed_eventargs.UserStream = true;
                    is_interval_changed = true;
                }
                if ( TimelinePeriodInt != int.Parse( TimelinePeriod.Text ) ) {
                    TimelinePeriodInt = int.Parse( TimelinePeriod.Text );
                    interval_changed_eventargs.Timeline = true;
                    is_interval_changed = true;
                }
                if ( DMPeriodInt != int.Parse( DMPeriod.Text ) ) {
                    DMPeriodInt = int.Parse( DMPeriod.Text );
                    interval_changed_eventargs.DirectMessage = true;
                    is_interval_changed = true;
                }
                if ( PubSearchPeriodInt != int.Parse( PubSearchPeriod.Text ) ) {
                    PubSearchPeriodInt = int.Parse( PubSearchPeriod.Text );
                    interval_changed_eventargs.PublicSearch = true;
                    is_interval_changed = true;
                }

                if ( ListsPeriodInt != int.Parse( ListsPeriod.Text ) ) {
                    ListsPeriodInt = int.Parse( ListsPeriod.Text );
                    interval_changed_eventargs.Lists = true;
                    is_interval_changed = true;
                }
                if ( ReplyPeriodInt != int.Parse( ReplyPeriod.Text ) ) {
                    ReplyPeriodInt = int.Parse( ReplyPeriod.Text );
                    interval_changed_eventargs.Reply = true;
                    is_interval_changed = true;
                }
                if ( UserTimelinePeriodInt != int.Parse( UserTimelinePeriod.Text ) ) {
                    UserTimelinePeriodInt = int.Parse( UserTimelinePeriod.Text );
                    interval_changed_eventargs.UserTimeline = true;
                    is_interval_changed = true;
                }

                if ( is_interval_changed && IntervalChanged != null ) {
                    IntervalChanged( this, interval_changed_eventargs );
                }

                Readed = StartupReaded.Checked;
                switch ( IconSize.SelectedIndex ) {
                case 0:
                    IconSz = MyCommon.IconSizes.IconNone;
                    break;
                case 1:
                    IconSz = MyCommon.IconSizes.Icon16;
                    break;
                case 2:
                    IconSz = MyCommon.IconSizes.Icon24;
                    break;
                case 3:
                    IconSz = MyCommon.IconSizes.Icon48;
                    break;
                case 4:
                    IconSz = MyCommon.IconSizes.Icon48_2;
                    break;
                }
                Status = StatusText.Text;
                PlaySound = PlaySnd.Checked;
                UnreadManage = UReadMng.Checked;
                OneWayLove = OneWayLv.Checked;

                FontUnread = lblUnread.Font;     //未使用
                ColorUnread = lblUnread.ForeColor;
                FontReaded = lblListFont.Font;     //リストフォントとして使用
                ColorReaded = lblListFont.ForeColor;
                ColorFav = lblFav.ForeColor;
                ColorOWL = lblOWL.ForeColor;
                ColorRetweet = lblRetweet.ForeColor;
                FontDetail = lblDetail.Font;
                ColorSelf = lblSelf.BackColor;
                ColorAtSelf = lblAtSelf.BackColor;
                ColorTarget = lblTarget.BackColor;
                ColorAtTarget = lblAtTarget.BackColor;
                ColorAtFromTarget = lblAtFromTarget.BackColor;
                ColorAtTo = lblAtTo.BackColor;
                ColorInputBackcolor = lblInputBackcolor.BackColor;
                ColorInputFont = lblInputFont.ForeColor;
                ColorListBackcolor = lblListBackcolor.BackColor;
                ColorDetailBackcolor = lblDetailBackcolor.BackColor;
                ColorDetail = lblDetail.ForeColor;
                ColorDetailLink = lblDetailLink.ForeColor;
                FontInputFont = lblInputFont.Font;
                switch ( cmbNameBalloon.SelectedIndex ) {
                case 0:
                    NameBalloon = MyCommon.NameBalloonEnum.None;
                    break;
                case 1:
                    NameBalloon = MyCommon.NameBalloonEnum.UserID;
                    break;
                case 2:
                    NameBalloon = MyCommon.NameBalloonEnum.NickName;
                    break;
                }

                switch ( ComboBoxPostKeySelect.SelectedIndex ) {
                case 2:
                    PostShiftEnter = true;
                    PostCtrlEnter = false;
                    break;
                case 1:
                    PostCtrlEnter = true;
                    PostShiftEnter = false;
                    break;
                case 0:
                    PostCtrlEnter = false;
                    PostShiftEnter = false;
                    break;
                }
                CountApi = int.Parse( TextCountApi.Text );
                CountApiReply = int.Parse( TextCountApiReply.Text );
                BrowserPath = BrowserPathText.Text.Trim();
                PostAndGet = CheckPostAndGet.Checked;
                UseRecommendStatus = CheckUseRecommendStatus.Checked;
                DispUsername = CheckDispUsername.Checked;
                CloseToExit = CheckCloseToExit.Checked;
                MinimizeToTray = CheckMinimizeToTray.Checked;
                switch ( ComboDispTitle.SelectedIndex ) {
                case 0:  //None
                    DispLatestPost = MyCommon.DispTitleEnum.None;
                    break;
                case 1:  //Ver
                    DispLatestPost = MyCommon.DispTitleEnum.Ver;
                    break;
                case 2:  //Post
                    DispLatestPost = MyCommon.DispTitleEnum.Post;
                    break;
                case 3:  //RepCount
                    DispLatestPost = MyCommon.DispTitleEnum.UnreadRepCount;
                    break;
                case 4:  //AllCount
                    DispLatestPost = MyCommon.DispTitleEnum.UnreadAllCount;
                    break;
                case 5:  //Rep+All
                    DispLatestPost = MyCommon.DispTitleEnum.UnreadAllRepCount;
                    break;
                case 6:  //Unread/All
                    DispLatestPost = MyCommon.DispTitleEnum.UnreadCountAllCount;
                    break;
                case 7: //Count of Status/Follow/Follower
                    DispLatestPost = MyCommon.DispTitleEnum.OwnStatus;
                    break;
                }
                SortOrderLock = CheckSortOrderLock.Checked;
                TinyUrlResolve = CheckTinyURL.Checked;
                ShortUrlForceResolve = CheckForceResolve.Checked;
                ShortUrl.IsResolve = TinyUrlResolve;
                ShortUrl.IsForceResolve = ShortUrlForceResolve;
                if ( RadioProxyNone.Checked ) {
                    my_proxy_type_ = HttpConnection.ProxyType.None;
                } else if ( RadioProxyIE.Checked ) {
                    my_proxy_type_ = HttpConnection.ProxyType.IE;
                } else {
                    my_proxy_type_ = HttpConnection.ProxyType.Specified;
                }
                ProxyAddress = TextProxyAddress.Text.Trim();
                ProxyPort = int.Parse( TextProxyPort.Text.Trim() );
                ProxyUser = TextProxyUser.Text.Trim();
                ProxyPassword = TextProxyPassword.Text.Trim();
                PeriodAdjust = CheckPeriodAdjust.Checked;
                StartupVersion = CheckStartupVersion.Checked;
                StartupFollowers = CheckStartupFollowers.Checked;
                RestrictFavCheck = CheckFavRestrict.Checked;
                AlwaysTop = CheckAlwaysTop.Checked;
                UrlConvertAuto = CheckAutoConvertUrl.Checked;
                ShortenTco = ShortenTcoCheck.Checked;
                OutputzEnabled = CheckOutputz.Checked;
                OutputzKey = TextBoxOutputzKey.Text.Trim();

                switch ( ComboBoxOutputzUrlmode.SelectedIndex ) {
                case 0:
                    OutputzUrlmode = MyCommon.OutputzUrlmode.twittercom;
                    break;
                case 1:
                    OutputzUrlmode = MyCommon.OutputzUrlmode.twittercomWithUsername;
                    break;
                }

                Nicoms = CheckNicoms.Checked;
                UseUnreadStyle = chkUnreadStyle.Checked;
                DateTimeFormat = CmbDateTimeFormat.Text;
                DefaultTimeOut = int.Parse( ConnectionTimeOut.Text );
                RetweetNoConfirm = CheckRetweetNoConfirm.Checked;
                LimitBalloon = CheckBalloonLimit.Checked;
                EventNotifyEnabled = CheckEventNotify.Checked;
                GetEventNotifyFlag( ref my_event_notify_flag_, ref is_my_event_notify_flag_ );
                ForceEventNotify = CheckForceEventNotify.Checked;
                FavEventUnread = CheckFavEventUnread.Checked;
                TranslateLanguage = (new Bing ()).GetLanguageEnumFromIndex( ComboBoxTranslateLanguage.SelectedIndex );
                EventSoundFile = (string)ComboBoxEventNotifySound.SelectedItem;
                AutoShortUrlFirst = (MyCommon.UrlConverter)ComboBoxAutoShortUrlFirst.SelectedIndex;
                TabIconDisp = chkTabIconDisp.Checked;
                ReadOwnPost = chkReadOwnPost.Checked;
                GetFav = chkGetFav.Checked;
                IsMonospace = CheckMonospace.Checked;
                ReadOldPosts = CheckReadOldPosts.Checked;
                UseSsl = CheckUseSsl.Checked;
                BitlyUser = TextBitlyId.Text;
                BitlyPwd = TextBitlyPw.Text;
                ShowGrid = CheckShowGrid.Checked;
                UseAtIdSupplement = CheckAtIdSupple.Checked;
                UseHashSupplement = CheckHashSupple.Checked;
                PreviewEnable = CheckPreviewEnable.Checked;
                TwitterApiUrl = TwitterAPIText.Text.Trim();
                TwitterSearchApiUrl = TwitterSearchAPIText.Text.Trim();
                switch ( ReplyIconStateCombo.SelectedIndex ) {
                case 0:
                    ReplyIconState = MyCommon.REPLY_ICONSTATE.None;
                    break;
                case 1:
                    ReplyIconState = MyCommon.REPLY_ICONSTATE.StaticIcon;
                    break;
                case 2:
                    ReplyIconState = MyCommon.REPLY_ICONSTATE.BlinkIcon;
                    break;
                }
                switch ( LanguageCombo.SelectedIndex ) {
                case 0:
                    Language = "OS";
                    break;
                case 1:
                    Language = "ja";
                    break;
                case 2:
                    Language = "en";
                    break;
                case 3:
                    Language = "zh-CN";
                    break;
                default:
                    Language = "en";
                    break;
                }
                hotkey_enabled_ = this.HotkeyCheck.Checked;
                hotkey_mod_ = Keys.None;
                if ( this.HotkeyAlt.Checked )
                    hotkey_mod_ = hotkey_mod_ | Keys.Alt;
                if ( this.HotkeyShift.Checked )
                    hotkey_mod_ = hotkey_mod_ | Keys.Shift;
                if ( this.HotkeyCtrl.Checked )
                    hotkey_mod_ = hotkey_mod_ | Keys.Control;
                if ( this.HotkeyWin.Checked )
                    hotkey_mod_ = hotkey_mod_ | Keys.LWin;
                int.TryParse( HotkeyCode.Text, out hotkey_value_ );
                hotkey_key_ = (Keys)HotkeyText.Tag;
                blink_new_mentions_ = ChkNewMentionsBlink.Checked;
                UseAdditionalCount = UseChangeGetCount.Checked;
                MoreCountApi = int.Parse( GetMoreTextCountApi.Text );
                FirstCountApi = int.Parse( FirstTextCountApi.Text );
                SearchCountApi = int.Parse( SearchTextCountApi.Text );
                FavoritesCountApi = int.Parse( FavoritesTextCountApi.Text );
                UserTimelineCountApi = int.Parse( UserTimelineTextCountApi.Text );
                ListCountApi = int.Parse( ListTextCountApi.Text );
                OpenUserTimeline = CheckOpenUserTimeline.Checked;
                ListDoubleClickAction = ListDoubleClickActionComboBox.SelectedIndex;
                UserAppointUrl = UserAppointUrlText.Text;
                this.hide_duplicated_retweets_ = this.HideDuplicatedRetweetsCheck.Checked;
                this.is_preview_foursquare_ = this.IsPreviewFoursquareCheckBox.Checked;
                this.foursquare_preview_height_ = int.Parse( this.FoursquarePreviewHeightTextBox.Text );
                this.foursquare_preview_width_ = int.Parse( this.FoursquarePreviewWidthTextBox.Text );
                this.foursquare_preview_zoom_ = int.Parse( this.FoursquarePreviewZoomTextBox.Text );
                this.is_list_statuses_include_rts_ = this.IsListsIncludeRtsCheckBox.Checked;
                this.tab_mouse_lock_ = this.TabMouseLockCheck.Checked;
                this.is_remove_same_event_ = this.IsRemoveSameFavEventCheckBox.Checked;
                this.is_notify_use_growl_ = this.IsNotifyUseGrowlCheckBox.Checked;
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.Save_ClickText3 );
                this.DialogResult = DialogResult.Cancel;
                
                return;
            }
        }


        private void Setting_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ( MyCommon._endingFlag )
                return;

            if ( this.DialogResult == DialogResult.Cancel ) {
                //キャンセル時は画面表示時のアカウントに戻す
                //キャンセル時でも認証済みアカウント情報は保存する
                this.user_accounts_.Clear();
                foreach ( object u in this.AuthUserCombo.Items ) {
                    this.user_accounts_.Add( (UserAccount)u );
                }
                //アクティブユーザーを起動時のアカウントに戻す（起動時アカウントなければ何もしない）
                bool user_set = false;
                if ( this.initial_user_id_ > 0 ) {
                    foreach ( UserAccount user_account in this.user_accounts_ ) {
                        if ( user_account.UserId == this.initial_user_id_ ) {
                            twitter_.Initialize( user_account.Token, user_account.TokenSecret, user_account.Username, user_account.UserId );
                            user_set = true;
                            
                            break;
                        }
                    }
                }
                //認証済みアカウントが削除されていた場合、もしくは起動時アカウントがなかった場合は、
                //アクティブユーザーなしとして初期化
                if ( !user_set ) {
                    twitter_.ClearAuthInfo();
                    twitter_.Initialize( string.Empty, string.Empty, string.Empty, 0 );
                }
            }

            if ( twitter_ != null && string.IsNullOrEmpty( twitter_.Username ) && e.CloseReason == CloseReason.None ) {
                if ( MessageBox.Show( Properties.Resources.Setting_FormClosing1, "Confirm", MessageBoxButtons.OKCancel, MessageBoxIcon.Question ) == DialogResult.Cancel ) {
                    e.Cancel = true;
                }
            }
            if ( validation_error_ ) {
                e.Cancel = true;
            }
            if ( e.Cancel == false && TreeViewSetting.SelectedNode != null ) {
                Panel curPanel = (Panel)TreeViewSetting.SelectedNode.Tag;
                curPanel.Visible = false;
                curPanel.Enabled = false;
            }
        }


        private void Setting_Load(object sender, EventArgs e)
        {
#if UA
            this.FollowCheckBox.Text = string.Format(this.FollowCheckBox.Text, ApplicationSettings.FeedbackTwitterName);
            this.GroupBox2.Visible = true;
#else
            this.GroupBox2.Visible = false;
#endif
            twitter_ = ((TweenMain)this.Owner).TwitterInstance;
            string username = twitter_.Username;
            string password = twitter_.Password;
            string access_token = twitter_.AccessToken;
            string access_token_secret = twitter_.AccessTokenSecret;
            //this.AuthStateLabel.Enabled = true;
            //this.AuthUserLabel.Enabled = true;
            this.AuthClearButton.Enabled = true;

            //if (tw.Username == "")
            //{
            //    //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click4
            //    //this.AuthUserLabel.Text = ""
            //    //this.Save.Enabled = false
            //}
            //else
            //{
            //    //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click3;
            //    //if (TwitterApiInfo.AccessLevel == ApiAccessLevel.ReadWrite)
            //    //{
            //    //    this.AuthStateLabel.Text += "(xAuth)";
            //    //}
            //    //else if (TwitterApiInfo.AccessLevel == ApiAccessLevel.ReadWriteAndDirectMessage)
            //    //{
            //    //    this.AuthStateLabel.Text += "(OAuth)";
            //    //}
            //    //this.AuthUserLabel.Text = tw.Username;
            //}

            this.AuthUserCombo.Items.Clear();
            if ( this.user_accounts_.Count > 0 ) {
                this.AuthUserCombo.Items.AddRange( this.user_accounts_.ToArray() );
                foreach ( UserAccount user_account in this.user_accounts_ ) {
                    if ( user_account.UserId == twitter_.UserId ) {
                        this.AuthUserCombo.SelectedItem = user_account;
                        this.initial_user_id_ = user_account.UserId;
                        break;
                    }
                }
            }

            this.StartupUserstreamCheck.Checked = UserstreamStartup;
            UserstreamPeriod.Text = UserstreamPeriodInt.ToString();
            TimelinePeriod.Text = TimelinePeriodInt.ToString();
            ReplyPeriod.Text = ReplyPeriodInt.ToString();
            DMPeriod.Text = DMPeriodInt.ToString();
            PubSearchPeriod.Text = PubSearchPeriodInt.ToString();
            ListsPeriod.Text = ListsPeriodInt.ToString();
            UserTimelinePeriod.Text = UserTimelinePeriodInt.ToString();

            StartupReaded.Checked = Readed;
            switch ( IconSz ) {
            case MyCommon.IconSizes.IconNone:
                IconSize.SelectedIndex = 0;
                break;
            case MyCommon.IconSizes.Icon16:
                IconSize.SelectedIndex = 1;
                break;
            case MyCommon.IconSizes.Icon24:
                IconSize.SelectedIndex = 2;
                break;
            case MyCommon.IconSizes.Icon48:
                IconSize.SelectedIndex = 3;
                break;
            case MyCommon.IconSizes.Icon48_2:
                IconSize.SelectedIndex = 4;
                break;
            }
            StatusText.Text = Status;
            UReadMng.Checked = UnreadManage;
            if ( UnreadManage == false ) {
                StartupReaded.Enabled = false;
            } else {
                StartupReaded.Enabled = true;
            }
            PlaySnd.Checked = PlaySound;
            OneWayLv.Checked = OneWayLove;

            lblListFont.Font = FontReaded;
            lblUnread.Font = FontUnread;
            lblUnread.ForeColor = ColorUnread;
            lblListFont.ForeColor = ColorReaded;
            lblFav.ForeColor = ColorFav;
            lblOWL.ForeColor = ColorOWL;
            lblRetweet.ForeColor = ColorRetweet;
            lblDetail.Font = FontDetail;
            lblSelf.BackColor = ColorSelf;
            lblAtSelf.BackColor = ColorAtSelf;
            lblTarget.BackColor = ColorTarget;
            lblAtTarget.BackColor = ColorAtTarget;
            lblAtFromTarget.BackColor = ColorAtFromTarget;
            lblAtTo.BackColor = ColorAtTo;
            lblInputBackcolor.BackColor = ColorInputBackcolor;
            lblInputFont.ForeColor = ColorInputFont;
            lblInputFont.Font = FontInputFont;
            lblListBackcolor.BackColor = ColorListBackcolor;
            lblDetailBackcolor.BackColor = ColorDetailBackcolor;
            lblDetail.ForeColor = ColorDetail;
            lblDetailLink.ForeColor = ColorDetailLink;

            switch ( NameBalloon ) {
            case MyCommon.NameBalloonEnum.None:
                cmbNameBalloon.SelectedIndex = 0;
                break;
            case MyCommon.NameBalloonEnum.UserID:
                cmbNameBalloon.SelectedIndex = 1;
                break;
            case MyCommon.NameBalloonEnum.NickName:
                cmbNameBalloon.SelectedIndex = 2;
                break;
            }

            if ( PostCtrlEnter ) {
                ComboBoxPostKeySelect.SelectedIndex = 1;
            } else if ( PostShiftEnter ) {
                ComboBoxPostKeySelect.SelectedIndex = 2;
            } else {
                ComboBoxPostKeySelect.SelectedIndex = 0;
            }

            TextCountApi.Text = CountApi.ToString();
            TextCountApiReply.Text = CountApiReply.ToString();
            BrowserPathText.Text = BrowserPath;
            CheckPostAndGet.Checked = PostAndGet;
            CheckUseRecommendStatus.Checked = UseRecommendStatus;
            CheckDispUsername.Checked = DispUsername;
            CheckCloseToExit.Checked = CloseToExit;
            CheckMinimizeToTray.Checked = MinimizeToTray;
            switch ( DispLatestPost ) {
            case MyCommon.DispTitleEnum.None:
                ComboDispTitle.SelectedIndex = 0;
                break;
            case MyCommon.DispTitleEnum.Ver:
                ComboDispTitle.SelectedIndex = 1;
                break;
            case MyCommon.DispTitleEnum.Post:
                ComboDispTitle.SelectedIndex = 2;
                break;
            case MyCommon.DispTitleEnum.UnreadRepCount:
                ComboDispTitle.SelectedIndex = 3;
                break;
            case MyCommon.DispTitleEnum.UnreadAllCount:
                ComboDispTitle.SelectedIndex = 4;
                break;
            case MyCommon.DispTitleEnum.UnreadAllRepCount:
                ComboDispTitle.SelectedIndex = 5;
                break;
            case MyCommon.DispTitleEnum.UnreadCountAllCount:
                ComboDispTitle.SelectedIndex = 6;
                break;
            case MyCommon.DispTitleEnum.OwnStatus:
                ComboDispTitle.SelectedIndex = 7;
                break;
            }
            CheckSortOrderLock.Checked = SortOrderLock;
            CheckTinyURL.Checked = TinyUrlResolve;
            CheckForceResolve.Checked = ShortUrlForceResolve;
            switch ( my_proxy_type_ ) {
            case HttpConnection.ProxyType.None:
                RadioProxyNone.Checked = true;
                break;
            case HttpConnection.ProxyType.IE:
                RadioProxyIE.Checked = true;
                break;
            default:
                RadioProxySpecified.Checked = true;
                break;
            }
            bool be_checked = RadioProxySpecified.Checked;
            LabelProxyAddress.Enabled = be_checked;
            TextProxyAddress.Enabled = be_checked;
            LabelProxyPort.Enabled = be_checked;
            TextProxyPort.Enabled = be_checked;
            LabelProxyUser.Enabled = be_checked;
            TextProxyUser.Enabled = be_checked;
            LabelProxyPassword.Enabled = be_checked;
            TextProxyPassword.Enabled = be_checked;

            TextProxyAddress.Text = ProxyAddress;
            TextProxyPort.Text = ProxyPort.ToString();
            TextProxyUser.Text = ProxyUser;
            TextProxyPassword.Text = ProxyPassword;

            CheckPeriodAdjust.Checked = PeriodAdjust;
            CheckStartupVersion.Checked = StartupVersion;
            CheckStartupFollowers.Checked = StartupFollowers;
            CheckFavRestrict.Checked = RestrictFavCheck;
            CheckAlwaysTop.Checked = AlwaysTop;
            CheckAutoConvertUrl.Checked = UrlConvertAuto;
            ShortenTcoCheck.Checked = ShortenTco;
            ShortenTcoCheck.Enabled = CheckAutoConvertUrl.Checked;
            CheckOutputz.Checked = OutputzEnabled;
            TextBoxOutputzKey.Text = OutputzKey;

            switch ( OutputzUrlmode ) {
            case MyCommon.OutputzUrlmode.twittercom:
                ComboBoxOutputzUrlmode.SelectedIndex = 0;
                break;
            case MyCommon.OutputzUrlmode.twittercomWithUsername:
                ComboBoxOutputzUrlmode.SelectedIndex = 1;
                break;
            }

            CheckNicoms.Checked = Nicoms;
            chkUnreadStyle.Checked = UseUnreadStyle;
            CmbDateTimeFormat.Text = DateTimeFormat;
            ConnectionTimeOut.Text = DefaultTimeOut.ToString();
            CheckRetweetNoConfirm.Checked = RetweetNoConfirm;
            CheckBalloonLimit.Checked = LimitBalloon;

            ApplyEventNotifyFlag( EventNotifyEnabled, EventNotifyFlag, IsMyEventNotifyFlag );
            CheckForceEventNotify.Checked = ForceEventNotify;
            CheckFavEventUnread.Checked = FavEventUnread;
            ComboBoxTranslateLanguage.SelectedIndex = (new Bing ()).GetIndexFromLanguageEnum( TranslateLanguage );
            SoundFileListup();
            ComboBoxAutoShortUrlFirst.SelectedIndex = (int)AutoShortUrlFirst;
            chkTabIconDisp.Checked = TabIconDisp;
            chkReadOwnPost.Checked = ReadOwnPost;
            chkGetFav.Checked = GetFav;
            CheckMonospace.Checked = IsMonospace;
            CheckReadOldPosts.Checked = ReadOldPosts;
            CheckUseSsl.Checked = UseSsl;
            TextBitlyId.Text = BitlyUser;
            TextBitlyPw.Text = BitlyPwd;
            TextBitlyId.Modified = false;
            TextBitlyPw.Modified = false;
            CheckShowGrid.Checked = ShowGrid;
            CheckAtIdSupple.Checked = UseAtIdSupplement;
            CheckHashSupple.Checked = UseHashSupplement;
            CheckPreviewEnable.Checked = PreviewEnable;
            TwitterAPIText.Text = TwitterApiUrl;
            TwitterSearchAPIText.Text = TwitterSearchApiUrl;
            switch ( ReplyIconState ) {
            case MyCommon.REPLY_ICONSTATE.None:
                ReplyIconStateCombo.SelectedIndex = 0;
                break;
            case MyCommon.REPLY_ICONSTATE.StaticIcon:
                ReplyIconStateCombo.SelectedIndex = 1;
                break;
            case MyCommon.REPLY_ICONSTATE.BlinkIcon:
                ReplyIconStateCombo.SelectedIndex = 2;
                break;
            }
            switch ( Language ) {
            case "OS":
                LanguageCombo.SelectedIndex = 0;
                break;
            case "ja":
                LanguageCombo.SelectedIndex = 1;
                break;
            case "en":
                LanguageCombo.SelectedIndex = 2;
                break;
            case "zh-CN":
                LanguageCombo.SelectedIndex = 3;
                break;
            default:
                LanguageCombo.SelectedIndex = 0;
                break;
            }
            HotkeyCheck.Checked = hotkey_enabled_;
            HotkeyAlt.Checked = ((hotkey_mod_ & Keys.Alt) == Keys.Alt);
            HotkeyCtrl.Checked = ((hotkey_mod_ & Keys.Control) == Keys.Control);
            HotkeyShift.Checked = ((hotkey_mod_ & Keys.Shift) == Keys.Shift);
            HotkeyWin.Checked = ((hotkey_mod_ & Keys.LWin) == Keys.LWin);
            HotkeyCode.Text = hotkey_value_.ToString();
            HotkeyText.Text = hotkey_key_.ToString();
            HotkeyText.Tag = hotkey_key_;
            HotkeyAlt.Enabled = hotkey_enabled_;
            HotkeyShift.Enabled = hotkey_enabled_;
            HotkeyCtrl.Enabled = hotkey_enabled_;
            HotkeyWin.Enabled = hotkey_enabled_;
            HotkeyText.Enabled = hotkey_enabled_;
            HotkeyCode.Enabled = hotkey_enabled_;
            ChkNewMentionsBlink.Checked = blink_new_mentions_;

            CheckOutputz_CheckedChanged( sender, e );

            GetMoreTextCountApi.Text = MoreCountApi.ToString();
            FirstTextCountApi.Text = FirstCountApi.ToString();
            SearchTextCountApi.Text = SearchCountApi.ToString();
            FavoritesTextCountApi.Text = FavoritesCountApi.ToString();
            UserTimelineTextCountApi.Text = UserTimelineCountApi.ToString();
            ListTextCountApi.Text = ListCountApi.ToString();
            UseChangeGetCount.Checked = UseAdditionalCount;
            Label28.Enabled = UseChangeGetCount.Checked;
            Label30.Enabled = UseChangeGetCount.Checked;
            Label53.Enabled = UseChangeGetCount.Checked;
            Label66.Enabled = UseChangeGetCount.Checked;
            Label17.Enabled = UseChangeGetCount.Checked;
            Label25.Enabled = UseChangeGetCount.Checked;
            GetMoreTextCountApi.Enabled = UseChangeGetCount.Checked;
            FirstTextCountApi.Enabled = UseChangeGetCount.Checked;
            SearchTextCountApi.Enabled = UseChangeGetCount.Checked;
            FavoritesTextCountApi.Enabled = UseChangeGetCount.Checked;
            UserTimelineTextCountApi.Enabled = UseChangeGetCount.Checked;
            ListTextCountApi.Enabled = UseChangeGetCount.Checked;
            CheckOpenUserTimeline.Checked = OpenUserTimeline;
            ListDoubleClickActionComboBox.SelectedIndex = ListDoubleClickAction;
            UserAppointUrlText.Text = UserAppointUrl;
            this.HideDuplicatedRetweetsCheck.Checked = this.hide_duplicated_retweets_;
            this.IsPreviewFoursquareCheckBox.Checked = this.is_preview_foursquare_;
            this.FoursquarePreviewHeightTextBox.Text = this.foursquare_preview_height_.ToString();
            this.FoursquarePreviewWidthTextBox.Text = this.foursquare_preview_width_.ToString();
            this.FoursquarePreviewZoomTextBox.Text = this.foursquare_preview_zoom_.ToString();
            this.IsListsIncludeRtsCheckBox.Checked = this.is_list_statuses_include_rts_;
            this.TabMouseLockCheck.Checked = this.tab_mouse_lock_;
            this.IsRemoveSameFavEventCheckBox.Checked = this.is_remove_same_event_;
            this.IsNotifyUseGrowlCheckBox.Checked = this.is_notify_use_growl_;

            if ( GrowlHelper.IsDllExists ) {
                IsNotifyUseGrowlCheckBox.Enabled = true;
            } else {
                IsNotifyUseGrowlCheckBox.Enabled = false;
            }

            this.TreeViewSetting.Nodes ["BasedNode"].Tag = BasedPanel;
            this.TreeViewSetting.Nodes ["BasedNode"].Nodes ["PeriodNode"].Tag = GetPeriodPanel;
            this.TreeViewSetting.Nodes ["BasedNode"].Nodes ["StartUpNode"].Tag = StartupPanel;
            this.TreeViewSetting.Nodes ["BasedNode"].Nodes ["GetCountNode"].Tag = GetCountPanel;
            //this.TreeViewSetting.Nodes["BasedNode"].Nodes["UserStreamNode"].Tag = UserStreamPanel;
            this.TreeViewSetting.Nodes ["ActionNode"].Tag = ActionPanel;
            this.TreeViewSetting.Nodes ["ActionNode"].Nodes ["TweetActNode"].Tag = TweetActPanel;
            this.TreeViewSetting.Nodes ["PreviewNode"].Tag = PreviewPanel;
            this.TreeViewSetting.Nodes ["PreviewNode"].Nodes ["TweetPrvNode"].Tag = TweetPrvPanel;
            this.TreeViewSetting.Nodes ["PreviewNode"].Nodes ["NotifyNode"].Tag = NotifyPanel;
            this.TreeViewSetting.Nodes ["FontNode"].Tag = FontPanel;
            this.TreeViewSetting.Nodes ["FontNode"].Nodes ["FontNode2"].Tag = FontPanel2;
            this.TreeViewSetting.Nodes ["ConnectionNode"].Tag = ConnectionPanel;
            this.TreeViewSetting.Nodes ["ConnectionNode"].Nodes ["ProxyNode"].Tag = ProxyPanel;
            this.TreeViewSetting.Nodes ["ConnectionNode"].Nodes ["CooperateNode"].Tag = CooperatePanel;
            this.TreeViewSetting.Nodes ["ConnectionNode"].Nodes ["ShortUrlNode"].Tag = ShortUrlPanel;

            this.TreeViewSetting.SelectedNode = this.TreeViewSetting.Nodes [0];
            this.TreeViewSetting.ExpandAll();

            //TreeViewSetting.SelectedNode = TreeViewSetting.TopNode;
            ActiveControl = StartAuthButton;
        }


        private void UserstreamPeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( UserstreamPeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.UserstreamPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period < 0 || period > 60 ) {
                MessageBox.Show( Properties.Resources.UserstreamPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void TimelinePeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( TimelinePeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TimelinePeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 15 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.TimelinePeriod_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void ReplyPeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( ReplyPeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TimelinePeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 15 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.TimelinePeriod_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void DMPeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( DMPeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 15 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void PubSearchPeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( PubSearchPeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.PubSearchPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 30 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.PubSearchPeriod_ValidatingText2 );
                e.Cancel = true;
            }
        }


        private void ListsPeriod_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( ListsPeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 15 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void UserTimeline_Validating(object sender, CancelEventArgs e)
        {
            int period;
            try {
                period = int.Parse( UserTimelinePeriod.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( period != 0 && (period < 15 || period > 6000) ) {
                MessageBox.Show( Properties.Resources.DMPeriod_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
            CalcApiUsing();
        }


        private void UReadMng_CheckedChanged(object sender, EventArgs e)
        {
            if ( UReadMng.Checked == true ) {
                StartupReaded.Enabled = true;
            } else {
                StartupReaded.Enabled = false;
            }
        }


        private void btnFontAndColor_Click(object sender, EventArgs e)
        { // Handles btnUnread.Click, btnDetail.Click, btnListFont.Click, btnInputFont.Click
            Button button = (Button)sender;
            DialogResult dr;

            FontDialog1.AllowVerticalFonts = false;
            FontDialog1.AllowScriptChange = true;
            FontDialog1.AllowSimulations = true;
            FontDialog1.AllowVectorFonts = true;
            FontDialog1.FixedPitchOnly = false;
            FontDialog1.FontMustExist = true;
            FontDialog1.ScriptsOnly = false;
            FontDialog1.ShowApply = false;
            FontDialog1.ShowEffects = true;
            FontDialog1.ShowColor = true;

            switch ( button.Name ) {
            case "btnUnread":
                FontDialog1.Color = lblUnread.ForeColor;
                FontDialog1.Font = lblUnread.Font;
                break;
            case "btnDetail":
                FontDialog1.Color = lblDetail.ForeColor;
                FontDialog1.Font = lblDetail.Font;
                break;
            case "btnListFont":
                FontDialog1.Color = lblListFont.ForeColor;
                FontDialog1.Font = lblListFont.Font;
                break;
            case "btnInputFont":
                FontDialog1.Color = lblInputFont.ForeColor;
                FontDialog1.Font = lblInputFont.Font;
                break;
            }

            try {
                dr = FontDialog1.ShowDialog();
            } catch ( ArgumentException ex ) {
                MessageBox.Show( ex.Message );
                return;
            }

            if ( dr == DialogResult.Cancel )
                return;

            switch ( button.Name ) {
            case "btnUnread":
                lblUnread.ForeColor = FontDialog1.Color;
                lblUnread.Font = FontDialog1.Font;
                break;
            case "btnDetail":
                lblDetail.ForeColor = FontDialog1.Color;
                lblDetail.Font = FontDialog1.Font;
                break;
            case "btnListFont":
                lblListFont.ForeColor = FontDialog1.Color;
                lblListFont.Font = FontDialog1.Font;
                break;
            case "btnInputFont":
                lblInputFont.ForeColor = FontDialog1.Color;
                lblInputFont.Font = FontDialog1.Font;
                break;
            }

        }


        private void btnColor_Click(object sender, EventArgs e)
        { //Handles btnSelf.Click, btnAtSelf.Click, btnTarget.Click, btnAtTarget.Click, btnAtFromTarget.Click, btnFav.Click, btnOWL.Click, btnInputBackcolor.Click, btnAtTo.Click, btnListBack.Click, btnDetailBack.Click, btnDetailLink.Click, btnRetweet.Click
            Button button = (Button)sender;
            DialogResult dr;

            ColorDialog1.AllowFullOpen = true;
            ColorDialog1.AnyColor = true;
            ColorDialog1.FullOpen = false;
            ColorDialog1.SolidColorOnly = false;

            switch ( button.Name ) {
            case "btnSelf":
                ColorDialog1.Color = lblSelf.BackColor;
                break;
            case "btnAtSelf":
                ColorDialog1.Color = lblAtSelf.BackColor;
                break;
            case "btnTarget":
                ColorDialog1.Color = lblTarget.BackColor;
                break;
            case "btnAtTarget":
                ColorDialog1.Color = lblAtTarget.BackColor;
                break;
            case "btnAtFromTarget":
                ColorDialog1.Color = lblAtFromTarget.BackColor;
                break;
            case "btnFav":
                ColorDialog1.Color = lblFav.ForeColor;
                break;
            case "btnOWL":
                ColorDialog1.Color = lblOWL.ForeColor;
                break;
            case "btnRetweet":
                ColorDialog1.Color = lblRetweet.ForeColor;
                break;
            case "btnInputBackcolor":
                ColorDialog1.Color = lblInputBackcolor.BackColor;
                break;
            case "btnAtTo":
                ColorDialog1.Color = lblAtTo.BackColor;
                break;
            case "btnListBack":
                ColorDialog1.Color = lblListBackcolor.BackColor;
                break;
            case "btnDetailBack":
                ColorDialog1.Color = lblDetailBackcolor.BackColor;
                break;
            case "btnDetailLink":
                ColorDialog1.Color = lblDetailLink.ForeColor;
                break;
            }

            dr = ColorDialog1.ShowDialog();

            if ( dr == DialogResult.Cancel )
                return;

            switch ( button.Name ) {
            case "btnSelf":
                lblSelf.BackColor = ColorDialog1.Color;
                break;
            case "btnAtSelf":
                lblAtSelf.BackColor = ColorDialog1.Color;
                break;
            case "btnTarget":
                lblTarget.BackColor = ColorDialog1.Color;
                break;
            case "btnAtTarget":
                lblAtTarget.BackColor = ColorDialog1.Color;
                break;
            case "btnAtFromTarget":
                lblAtFromTarget.BackColor = ColorDialog1.Color;
                break;
            case "btnFav":
                lblFav.ForeColor = ColorDialog1.Color;
                break;
            case "btnOWL":
                lblOWL.ForeColor = ColorDialog1.Color;
                break;
            case "btnRetweet":
                lblRetweet.ForeColor = ColorDialog1.Color;
                break;
            case "btnInputBackcolor":
                lblInputBackcolor.BackColor = ColorDialog1.Color;
                break;
            case "btnAtTo":
                lblAtTo.BackColor = ColorDialog1.Color;
                break;
            case "btnListBack":
                lblListBackcolor.BackColor = ColorDialog1.Color;
                break;
            case "btnDetailBack":
                lblDetailBackcolor.BackColor = ColorDialog1.Color;
                break;
            case "btnDetailLink":
                lblDetailLink.ForeColor = ColorDialog1.Color;
                break;
            }
        }


        public int UserstreamPeriodInt { get; set; }


        public bool UserstreamStartup { get; set; }


        public int TimelinePeriodInt { get; set; }


        public int ReplyPeriodInt { get; set; }


        public int DMPeriodInt { get; set; }


        public int PubSearchPeriodInt { get; set; }


        public int ListsPeriodInt { get; set; }


        public int UserTimelinePeriodInt { get; set; }


        public bool Readed { get; set; }


        public MyCommon.IconSizes IconSz { get; set; }


        public string Status { get; set; }


        public bool UnreadManage { get; set; }


        public bool PlaySound { get; set; }


        public bool OneWayLove { get; set; }


        public Font FontUnread { get; set; } /////未使用
        public Color ColorUnread { get; set; }


        public Font FontReaded { get; set; } /////リストフォントとして使用
        public Color ColorReaded { get; set; }


        public Color ColorFav { get; set; }


        public Color ColorOWL { get; set; }


        public Color ColorRetweet { get; set; }


        public Font FontDetail { get; set; }


        public Color ColorDetail { get; set; }


        public Color ColorDetailLink { get; set; }


        public Color ColorSelf { get; set; }


        public Color ColorAtSelf { get; set; }


        public Color ColorTarget { get; set; }


        public Color ColorAtTarget { get; set; }


        public Color ColorAtFromTarget { get; set; }


        public Color ColorAtTo { get; set; }


        public Color ColorInputBackcolor { get; set; }


        public Color ColorInputFont { get; set; }


        public Font FontInputFont { get; set; }


        public Color ColorListBackcolor { get; set; }


        public Color ColorDetailBackcolor { get; set; }


        public MyCommon.NameBalloonEnum NameBalloon { get; set; }


        public bool PostCtrlEnter { get; set; }


        public bool PostShiftEnter { get; set; }


        public int CountApi { get; set; }


        public int CountApiReply { get; set; }


        public int MoreCountApi { get; set; }


        public int FirstCountApi { get; set; }


        public int SearchCountApi { get; set; }


        public int FavoritesCountApi { get; set; }


        public int UserTimelineCountApi { get; set; }


        public int ListCountApi { get; set; }


        public bool PostAndGet { get; set; }


        public bool UseRecommendStatus { get; set; }


        public string RecommendStatusText { get; set; }


        public bool DispUsername { get; set; }


        public bool CloseToExit { get; set; }


        public bool MinimizeToTray { get; set; }


        public MyCommon.DispTitleEnum DispLatestPost { get; set; }


        public string BrowserPath { get; set; }


        public bool TinyUrlResolve { get; set; }


        public bool ShortUrlForceResolve { get; set; }


        private void CheckUseRecommendStatus_CheckedChanged(object sender, EventArgs e)
        {
            if ( CheckUseRecommendStatus.Checked == true ) {
                StatusText.Enabled = false;
            } else {
                StatusText.Enabled = true;
            }
        }


        public bool SortOrderLock { get; set; }


        public HttpConnection.ProxyType SelectedProxyType {
            get {
                return my_proxy_type_;
            }
            set {
                my_proxy_type_ = value;
            }
        }


        public string ProxyAddress { get; set; }


        public int ProxyPort { get; set; }


        public string ProxyUser { get; set; }


        public string ProxyPassword { get; set; }


        public bool PeriodAdjust { get; set; }


        public bool StartupVersion { get; set; }


        public bool StartupFollowers { get; set; }


        public bool RestrictFavCheck { get; set; }


        public bool AlwaysTop { get; set; }


        public bool UrlConvertAuto { get; set; }


        public bool ShortenTco { get; set; }


        public bool OutputzEnabled { get; set; }


        public string OutputzKey { get; set; }


        public MyCommon.OutputzUrlmode OutputzUrlmode { get; set; }


        public bool Nicoms { get; set; }


        public MyCommon.UrlConverter AutoShortUrlFirst { get; set; }


        public bool UseUnreadStyle { get; set; }


        public string DateTimeFormat { get; set; }


        public int DefaultTimeOut { get; set; }


        public bool RetweetNoConfirm { get; set; }


        public bool TabIconDisp { get; set; }


        public MyCommon.REPLY_ICONSTATE ReplyIconState { get; set; }


        public bool ReadOwnPost { get; set; }


        public bool GetFav { get; set; }


        public bool IsMonospace { get; set; }


        public bool ReadOldPosts { get; set; }


        public bool UseSsl { get; set; }


        public string BitlyUser { get; set; }


        public string BitlyPwd { get; set; }


        public bool ShowGrid { get; set; }


        public bool UseAtIdSupplement { get; set; }


        public bool UseHashSupplement { get; set; }


        public bool PreviewEnable { get; set; }


        public bool UseAdditionalCount { get; set; }


        public bool OpenUserTimeline { get; set; }


        public string TwitterApiUrl { get; set; }


        public string TwitterSearchApiUrl { get; set; }


        public string Language { get; set; }


        private void Button3_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open_file_dialog = new OpenFileDialog()) {
                open_file_dialog.Filter = Properties.Resources.Button3_ClickText1;
                open_file_dialog.FilterIndex = 1;
                open_file_dialog.Title = Properties.Resources.Button3_ClickText2;
                open_file_dialog.RestoreDirectory = true;

                if ( open_file_dialog.ShowDialog() == DialogResult.OK ) {
                    BrowserPathText.Text = open_file_dialog.FileName;
                }
            }
        }


        private void RadioProxySpecified_CheckedChanged(object sender, EventArgs e)
        {
            bool radio_proxy_specified_checked = RadioProxySpecified.Checked;
            LabelProxyAddress.Enabled = radio_proxy_specified_checked;
            TextProxyAddress.Enabled = radio_proxy_specified_checked;
            LabelProxyPort.Enabled = radio_proxy_specified_checked;
            TextProxyPort.Enabled = radio_proxy_specified_checked;
            LabelProxyUser.Enabled = radio_proxy_specified_checked;
            TextProxyUser.Enabled = radio_proxy_specified_checked;
            LabelProxyPassword.Enabled = radio_proxy_specified_checked;
            TextProxyPassword.Enabled = radio_proxy_specified_checked;
        }


        private void TextProxyPort_Validating(object sender, CancelEventArgs e)
        {
            int port;
            if ( string.IsNullOrWhiteSpace( TextProxyPort.Text ) )
                TextProxyPort.Text = "0";
            if ( int.TryParse( TextProxyPort.Text.Trim(), out port ) == false ) {
                MessageBox.Show( Properties.Resources.TextProxyPort_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }
            if ( port < 0 || port > 65535 ) {
                MessageBox.Show( Properties.Resources.TextProxyPort_ValidatingText2 );
                e.Cancel = true;
                
                return;
            }
        }


        private void CheckOutputz_CheckedChanged(object sender, EventArgs e)
        {
            if ( CheckOutputz.Checked == true ) {
                Label59.Enabled = true;
                Label60.Enabled = true;
                TextBoxOutputzKey.Enabled = true;
                ComboBoxOutputzUrlmode.Enabled = true;
            } else {
                Label59.Enabled = false;
                Label60.Enabled = false;
                TextBoxOutputzKey.Enabled = false;
                ComboBoxOutputzUrlmode.Enabled = false;
            }
        }


        private void TextBoxOutputzKey_Validating(object sender, CancelEventArgs e)
        {
            if ( CheckOutputz.Checked ) {
                TextBoxOutputzKey.Text = TextBoxOutputzKey.Text.Trim();
                if ( TextBoxOutputzKey.Text.Length == 0 ) {
                    MessageBox.Show( Properties.Resources.TextBoxOutputzKey_Validating );
                    e.Cancel = true;
                    return;
                }
            }
        }


        private bool CreateDateTimeFormatSample()
        {
            try {
                LabelDateTimeFormatApplied.Text = DateTime.Now.ToString( CmbDateTimeFormat.Text );
            } catch ( FormatException ) {
                LabelDateTimeFormatApplied.Text = Properties.Resources.CreateDateTimeFormatSampleText1;
                return false;
            }
            return true;
        }


        private void CmbDateTimeFormat_TextUpdate(object sender, EventArgs e)
        {
            CreateDateTimeFormatSample();
        }


        private void CmbDateTimeFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            CreateDateTimeFormatSample();
        }


        private void CmbDateTimeFormat_Validating(object sender, CancelEventArgs e)
        {
            if ( !CreateDateTimeFormatSample() ) {
                MessageBox.Show( Properties.Resources.CmbDateTimeFormat_Validating );
                e.Cancel = true;
            }
        }


        private void ConnectionTimeOut_Validating(object sender, CancelEventArgs e)
        {
            int connection_timeout;
            try {
                connection_timeout = int.Parse( ConnectionTimeOut.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.ConnectionTimeOut_ValidatingText1 );
                e.Cancel = true;
                
                return;
            }

            if ( connection_timeout < (int)MyCommon.HttpTimeOut.MinValue || connection_timeout > (int)MyCommon.HttpTimeOut.MaxValue ) {
                MessageBox.Show( Properties.Resources.ConnectionTimeOut_ValidatingText1 );
                e.Cancel = true;
            }
        }


        private void LabelDateTimeFormatApplied_VisibleChanged(object sender, EventArgs e)
        {
            CreateDateTimeFormatSample();
        }


        private void TextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( TextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count < 20 || count > 200 ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void TextCountApiReply_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( TextCountApiReply.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count < 20 || count > 200 ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        public bool LimitBalloon { get; set; }


        public bool EventNotifyEnabled { get; set; }


        public MyCommon.EVENTTYPE EventNotifyFlag {
            get {
                return my_event_notify_flag_;
            }
            set {
                my_event_notify_flag_ = value;
            }
        }


        public MyCommon.EVENTTYPE IsMyEventNotifyFlag {
            get {
                return is_my_event_notify_flag_;
            }
            set {
                is_my_event_notify_flag_ = value;
            }
        }


        public bool ForceEventNotify { get; set; }


        public bool FavEventUnread { get; set; }


        public string TranslateLanguage {
            get {
                return my_translated_retweets_;
            }
            set {
                my_translated_retweets_ = value;
                ComboBoxTranslateLanguage.SelectedIndex = (new Bing ()).GetIndexFromLanguageEnum( value );
            }
        }


        public string EventSoundFile { get; set; }


        public int ListDoubleClickAction { get; set; }


        public string UserAppointUrl { get; set; }


        private void ComboBoxAutoShortUrlFirst_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ( ComboBoxAutoShortUrlFirst.SelectedIndex == (int)MyCommon.UrlConverter.Bitly ||
               ComboBoxAutoShortUrlFirst.SelectedIndex == (int)MyCommon.UrlConverter.Jmp ) {
                Label76.Enabled = true;
                Label77.Enabled = true;
                TextBitlyId.Enabled = true;
                TextBitlyPw.Enabled = true;
            } else {
                Label76.Enabled = false;
                Label77.Enabled = false;
                TextBitlyId.Enabled = false;
                TextBitlyPw.Enabled = false;
            }
        }


        private void ButtonBackToDefaultFontColor_Click(object sender, EventArgs e)
        { //Handles ButtonBackToDefaultFontColor.Click, ButtonBackToDefaultFontColor2.Click
            lblUnread.ForeColor = SystemColors.ControlText;
            lblUnread.Font = new Font (SystemFonts.DefaultFont, FontStyle.Bold | FontStyle.Underline);

            lblListFont.ForeColor = System.Drawing.SystemColors.ControlText;
            lblListFont.Font = System.Drawing.SystemFonts.DefaultFont;

            lblDetail.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.ControlText );
            lblDetail.Font = System.Drawing.SystemFonts.DefaultFont;

            lblInputFont.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.ControlText );
            lblInputFont.Font = System.Drawing.SystemFonts.DefaultFont;

            lblSelf.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.AliceBlue );

            lblAtSelf.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.AntiqueWhite );

            lblTarget.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.LemonChiffon );

            lblAtTarget.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.LavenderBlush );

            lblAtFromTarget.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.Honeydew );

            lblFav.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.Red );

            lblOWL.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.Blue );

            lblInputBackcolor.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.LemonChiffon );

            lblAtTo.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.Pink );

            lblListBackcolor.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.Window );

            lblDetailBackcolor.BackColor = Color.FromKnownColor( System.Drawing.KnownColor.Window );

            lblDetailLink.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.Blue );

            lblRetweet.ForeColor = Color.FromKnownColor( System.Drawing.KnownColor.Green );
        }


        private bool StartAuth()
        {
            //現在の設定内容で通信
            HttpConnection.ProxyType proxy_type;
            if ( RadioProxyNone.Checked ) {
                proxy_type = HttpConnection.ProxyType.None;
            } else if ( RadioProxyIE.Checked ) {
                proxy_type = HttpConnection.ProxyType.IE;
            } else {
                proxy_type = HttpConnection.ProxyType.Specified;
            }
            //string padr = TextProxyAddress.Text.Trim();
            string proxy_address_trimed = TextProxyAddress.Text.Trim();
            //int pport = int.Parse( TextProxyPort.Text.Trim() );
            int proxy_port = int.Parse( TextProxyPort.Text.Trim() );
            //string pusr = TextProxyUser.Text.Trim();
            string proxy_username = TextProxyUser.Text.Trim();
            //string ppw = TextProxyPassword.Text.Trim();
            string proxy_password = TextProxyPassword.Text.Trim();

            //通信基底クラス初期化
            HttpConnection.InitializeConnection( 20, proxy_type, proxy_address_trimed, proxy_port, proxy_username, proxy_password );
            HttpTwitter.TwitterUrl = TwitterAPIText.Text.Trim();
            HttpTwitter.TwitterSearchUrl = TwitterSearchAPIText.Text.Trim();
            twitter_.Initialize( string.Empty, string.Empty, string.Empty, 0 );
            //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click4;
            //this.AuthUserLabel.Text = "";
            //string pinPageUrl = "";
            string pin_page_url = string.Empty;
            string result = twitter_.StartAuthentication( ref pin_page_url );
            if ( string.IsNullOrEmpty( result ) ) {
                using (AuthBrowser auth_browser = new AuthBrowser()) {
                    auth_browser.Auth = true;
                    auth_browser.UrlString = pin_page_url;
                    if ( auth_browser.ShowDialog( this ) == DialogResult.OK ) {
                        this.pin_ = auth_browser.PinString;
                        
                        return true;
                    } else {
                        return false;
                    }
                }
            } else {
                MessageBox.Show( Properties.Resources.AuthorizeButton_Click2 + Environment.NewLine + result, "Authenticate", MessageBoxButtons.OK );
                
                return false;
            }
        }


        private bool PinAuth()
        {
            string pin_code = this.pin_;   //PIN Code

            string result = twitter_.Authenticate( pin_code );
            if ( string.IsNullOrEmpty( result ) ) {
                MessageBox.Show( Properties.Resources.AuthorizeButton_Click1, "Authenticate", MessageBoxButtons.OK );
                //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click3;
                //this.AuthUserLabel.Text = tw.Username;
                int index = -1;
                UserAccount user = new UserAccount ();
                user.Username = twitter_.Username;
                user.UserId = twitter_.UserId;
                user.Token = twitter_.AccessToken;
                user.TokenSecret = twitter_.AccessTokenSecret;

                foreach ( object that in this.AuthUserCombo.Items ) {
                    UserAccount user_account = (UserAccount)that;
                    if ( user_account.Username.ToLower() == twitter_.Username.ToLower() ) {
                        index = this.AuthUserCombo.Items.IndexOf( user_account );
                        
                        break;
                    }
                }
                if ( index > -1 ) {
                    this.AuthUserCombo.Items.RemoveAt( index );
                    this.AuthUserCombo.Items.Insert( index, user );
                    this.AuthUserCombo.SelectedIndex = index;
                } else {
                    this.AuthUserCombo.SelectedIndex = this.AuthUserCombo.Items.Add( user );
                }
                //if (TwitterApiInfo.AccessLevel = ApiAccessLevel.ReadWrite)
                //{
                //    this.AuthStateLabel.Text += "(xAuth)";
                //}
                //else if (TwitterApiInfo.AccessLevel == ApiAccessLevel.ReadWriteAndDirectMessage)
                //{
                //    this.AuthStateLabel.Text += "(OAuth)";
                //}
                return true;
            } else {
                MessageBox.Show( Properties.Resources.AuthorizeButton_Click2 + Environment.NewLine + result, "Authenticate", MessageBoxButtons.OK );
                //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click4;
                //this.AuthUserLabel.Text = "";
                return false;
            }
        }


        private void StartAuthButton_Click(object sender, EventArgs e)
        {
            //this.Save.Enabled = false;
            if ( StartAuth() ) {
                if ( PinAuth() ) {
                    CalcApiUsing();
                    //this.Save.Enabled = true;
                }
            }
        }


        private void AuthClearButton_Click(object sender, EventArgs e)
        {
            //tw.ClearAuthInfo();
            //this.AuthStateLabel.Text = Properties.Resources.AuthorizeButton_Click4;
            //this.AuthUserLabel.Text = "";
            if ( this.AuthUserCombo.SelectedIndex > -1 ) {
                this.AuthUserCombo.Items.RemoveAt( this.AuthUserCombo.SelectedIndex );
                if ( this.AuthUserCombo.Items.Count > 0 ) {
                    this.AuthUserCombo.SelectedIndex = 0;
                } else {
                    this.AuthUserCombo.SelectedIndex = -1;
                }
            }
            //this.Save.Enabled = false;
            CalcApiUsing();
        }


        private void DisplayApiMaxCount()
        {
            if ( MyCommon.TwitterApiInfo.MaxCount > -1 ) {
                LabelApiUsing.Text = string.Format( Properties.Resources.SettingAPIUse1, MyCommon.TwitterApiInfo.UsingCount, MyCommon.TwitterApiInfo.MaxCount );
            } else {
                LabelApiUsing.Text = string.Format( Properties.Resources.SettingAPIUse1, MyCommon.TwitterApiInfo.UsingCount, "???" );
            }
        }


        private void CalcApiUsing()
        {
            int tmp;
            int using_api = 0;
            //int ListsTabNum = 0;
            int lists_tab_amount = 0;
            //int UserTimelineTabNum = 0;
            int user_timeline_tab_amout = 0;
            //int ApiLists = 0;
            int lists_api = 0;
            //int ApiUserTimeline = 0;
            int user_timeline_api = 0;

            try {
                // 初回起動時などにnullの場合あり
                lists_tab_amount = TabInformations.GetInstance().GetTabsByType( MyCommon.TabUsageType.Lists ).Count;
            } catch ( Exception ) {
                return;
            }

            try {
                // 初回起動時などにnullの場合あり
                user_timeline_tab_amout = TabInformations.GetInstance().GetTabsByType( MyCommon.TabUsageType.UserTimeline ).Count;
            } catch ( Exception ) {
                return;
            }

            // Recent計算 0は手動更新
            if ( int.TryParse( TimelinePeriod.Text, out tmp ) ) {
                if ( tmp != 0 ) {
                    using_api += 3600 / tmp;
                }
            }

            // Reply計算 0は手動更新
            if ( int.TryParse( ReplyPeriod.Text, out tmp ) ) {
                if ( tmp != 0 ) {
                    using_api += 3600 / tmp;
                }
            }

            // DM計算 0は手動更新 送受信両方
            if ( int.TryParse( DMPeriod.Text, out tmp ) ) {
                if ( tmp != 0 ) {
                    using_api += (3600 / tmp) * 2;
                }
            }

            // Listsタブ計算 0は手動更新
            if ( int.TryParse( ListsPeriod.Text, out tmp ) ) {
                if ( tmp != 0 ) {
                    lists_api = (3600 / tmp) * lists_tab_amount;
                    using_api += lists_api;
                }
            }

            // UserTimelineタブ計算 0は手動更新
            if ( int.TryParse( UserTimelinePeriod.Text, out tmp ) ) {
                if ( tmp != 0 ) {
                    user_timeline_api = (3600 / tmp) * user_timeline_tab_amout;
                    using_api += user_timeline_api;
                }
            }

            if ( twitter_ != null ) {
                if ( MyCommon.TwitterApiInfo.MaxCount == -1 ) {
                    if ( Twitter.AccountState == MyCommon.ACCOUNT_STATE.Valid ) {
                        MyCommon.TwitterApiInfo.UsingCount = using_api;
                        Thread proc = new Thread (new System.Threading.ThreadStart (() => {
                            twitter_.GetInfoApi( null ); //取得エラー時はinfoCountは初期状態（値：-1）
                            if ( this.IsHandleCreated && !this.IsDisposed )
                                Invoke( new MethodInvoker (DisplayApiMaxCount) );
                        }));
                        proc.Start();
                    } else {
                        LabelApiUsing.Text = string.Format( Properties.Resources.SettingAPIUse1, using_api, "???" );
                    }
                } else {
                    LabelApiUsing.Text = string.Format( Properties.Resources.SettingAPIUse1, using_api, MyCommon.TwitterApiInfo.MaxCount );
                }
            }


            LabelPostAndGet.Visible = CheckPostAndGet.Checked && !twitter_.UserStreamEnabled;
            LabelUserStreamActive.Visible = twitter_.UserStreamEnabled;

            LabelApiUsingUserStreamEnabled.Text = string.Format( Properties.Resources.SettingAPIUse2, (lists_api + user_timeline_api).ToString() );
            LabelApiUsingUserStreamEnabled.Visible = twitter_.UserStreamEnabled;
        }


        private void CheckPostAndGet_CheckedChanged(object sender, EventArgs e)
        {
            CalcApiUsing();
        }


        private void Setting_Shown(object sender, EventArgs e)
        {
            do {
                Thread.Sleep( 10 );
                if ( this.Disposing || this.IsDisposed )
                    return;
            } while (!this.IsHandleCreated);
            this.TopMost = this.AlwaysTop;
            CalcApiUsing();
        }


        private void ButtonApiCalc_Click(object sender, EventArgs e)
        {
            CalcApiUsing();
        }


        public static AppendSettingDialog Instance {
            get { return _instance; }
        }


        private bool BitlyValidation(string id, string apikey)
        {
            if ( string.IsNullOrEmpty( id ) || string.IsNullOrEmpty( apikey ) ) {
                return false;
            }

            string request_url = "http://api.bit.ly/v3/validate";
            string content = string.Empty;
            IDictionary<string, string> param = new Dictionary<string, string>();

            param.Add( "login", ApplicationSettings.BitlyLoginId );
            param.Add( "apiKey", ApplicationSettings.BitlyApiKey );
            param.Add( "x_login", id );
            param.Add( "x_apiKey", apikey );
            param.Add( "format", "txt" );

            if ( !(new HttpVarious ()).PostData( request_url, param, out content ) ) {
                return true;             // 通信エラーの場合はとりあえずチェックを通ったことにする
            } else if ( content.Trim() == "1" ) {
                return true;             // 検証成功
            } else if ( content.Trim() == "0" ) {
                return false;            // 検証失敗 APIキーとIDの組み合わせが違う
            } else {
                return true;             // 規定外応答：通信エラーの可能性があるためとりあえずチェックを通ったことにする
            }
        }


        private void Cancel_Click(object sender, EventArgs e)
        {
            validation_error_ = false;
        }


        public bool hotkey_enabled_;
        public Keys hotkey_key_;
        public int hotkey_value_;
        public Keys hotkey_mod_;


        private void HotkeyText_KeyDown(object sender, KeyEventArgs e)
        {
            //KeyValueで判定する。
            //表示文字とのテーブルを用意すること
            HotkeyText.Text = e.KeyCode.ToString();
            HotkeyCode.Text = e.KeyValue.ToString();
            HotkeyText.Tag = e.KeyCode;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }


        private void HotkeyCheck_CheckedChanged(object sender, EventArgs e)
        {
            HotkeyCtrl.Enabled = HotkeyCheck.Checked;
            HotkeyAlt.Enabled = HotkeyCheck.Checked;
            HotkeyShift.Enabled = HotkeyCheck.Checked;
            HotkeyWin.Enabled = HotkeyCheck.Checked;
            HotkeyText.Enabled = HotkeyCheck.Checked;
            HotkeyCode.Enabled = HotkeyCheck.Checked;
        }


        public bool blink_new_mentions_;


        private void GetMoreTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( GetMoreTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 200) ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void UseChangeGetCount_CheckedChanged(object sender, EventArgs e)
        {
            GetMoreTextCountApi.Enabled = UseChangeGetCount.Checked;
            FirstTextCountApi.Enabled = UseChangeGetCount.Checked;
            Label28.Enabled = UseChangeGetCount.Checked;
            Label30.Enabled = UseChangeGetCount.Checked;
            Label53.Enabled = UseChangeGetCount.Checked;
            Label66.Enabled = UseChangeGetCount.Checked;
            Label17.Enabled = UseChangeGetCount.Checked;
            Label25.Enabled = UseChangeGetCount.Checked;
            SearchTextCountApi.Enabled = UseChangeGetCount.Checked;
            FavoritesTextCountApi.Enabled = UseChangeGetCount.Checked;
            UserTimelineTextCountApi.Enabled = UseChangeGetCount.Checked;
            ListTextCountApi.Enabled = UseChangeGetCount.Checked;
        }


        private void FirstTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( FirstTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 200) ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void SearchTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( SearchTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextSearchCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 100) ) {
                MessageBox.Show( Properties.Resources.TextSearchCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void FavoritesTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( FavoritesTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 200) ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void UserTimelineTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( UserTimelineTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 200) ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }


        private void ListTextCountApi_Validating(object sender, CancelEventArgs e)
        {
            int count;
            try {
                count = int.Parse( ListTextCountApi.Text );
            } catch ( Exception ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }

            if ( count != 0 && (count < 20 || count > 200) ) {
                MessageBox.Show( Properties.Resources.TextCountApi_Validating1 );
                e.Cancel = true;
                
                return;
            }
        }

        //private void CheckEventNotify_CheckedChanged(object sender, EventArgs e)
        //                Handles CheckEventNotify.CheckedChanged, CheckFavoritesEvent.CheckStateChanged,
        //                        CheckUnfavoritesEvent.CheckStateChanged, CheckFollowEvent.CheckStateChanged,
        //                        CheckListMemberAddedEvent.CheckStateChanged, CheckListMemberRemovedEvent.CheckStateChanged,
        //                        CheckListCreatedEvent.CheckStateChanged, CheckUserUpdateEvent.CheckStateChanged
        //{
        //    EventNotifyEnabled = CheckEventNotify.Checked;
        //    GetEventNotifyFlag(EventNotifyFlag, IsMyEventNotifyFlag);
        //    ApplyEventNotifyFlag(EventNotifyEnabled, EventNotifyFlag, IsMyEventNotifyFlag);
        //}

        private class EventCheckboxTableElement
        {
            public CheckBox CheckBox;
            public MyCommon.EVENTTYPE Type;
        }

        private EventCheckboxTableElement[] GetEventCheckboxTable()
        {
            //EventCheckboxTableElement[] _eventCheckboxTable = new EventCheckboxTableElement[8];
            EventCheckboxTableElement[] event_checkbox_table = new EventCheckboxTableElement[8];

            event_checkbox_table [0] = new EventCheckboxTableElement ();
            event_checkbox_table [0].CheckBox = CheckFavoritesEvent;
            event_checkbox_table [0].Type = MyCommon.EVENTTYPE.Favorite;

            event_checkbox_table [1] = new EventCheckboxTableElement ();
            event_checkbox_table [1].CheckBox = CheckUnfavoritesEvent;
            event_checkbox_table [1].Type = MyCommon.EVENTTYPE.Unfavorite;

            event_checkbox_table [2] = new EventCheckboxTableElement ();
            event_checkbox_table [2].CheckBox = CheckFollowEvent;
            event_checkbox_table [2].Type = MyCommon.EVENTTYPE.Follow;

            event_checkbox_table [3] = new EventCheckboxTableElement ();
            event_checkbox_table [3].CheckBox = CheckListMemberAddedEvent;
            event_checkbox_table [3].Type = MyCommon.EVENTTYPE.ListMemberAdded;

            event_checkbox_table [4] = new EventCheckboxTableElement ();
            event_checkbox_table [4].CheckBox = CheckListMemberRemovedEvent;
            event_checkbox_table [4].Type = MyCommon.EVENTTYPE.ListMemberRemoved;

            event_checkbox_table [5] = new EventCheckboxTableElement ();
            event_checkbox_table [5].CheckBox = CheckBlockEvent;
            event_checkbox_table [5].Type = MyCommon.EVENTTYPE.Block;

            event_checkbox_table [6] = new EventCheckboxTableElement ();
            event_checkbox_table [6].CheckBox = CheckUserUpdateEvent;
            event_checkbox_table [6].Type = MyCommon.EVENTTYPE.UserUpdate;

            event_checkbox_table [7] = new EventCheckboxTableElement ();
            event_checkbox_table [7].CheckBox = CheckListCreatedEvent;
            event_checkbox_table [7].Type = MyCommon.EVENTTYPE.ListCreated;

            return event_checkbox_table;
        }


        private void GetEventNotifyFlag(ref MyCommon.EVENTTYPE eventnotifyflag, ref MyCommon.EVENTTYPE isMyeventnotifyflag)
        {
            MyCommon.EVENTTYPE event_type = MyCommon.EVENTTYPE.None;
            MyCommon.EVENTTYPE my_event_type = MyCommon.EVENTTYPE.None;

            foreach ( EventCheckboxTableElement table_element in GetEventCheckboxTable() ) {
                switch ( table_element.CheckBox.CheckState ) {
                case CheckState.Checked:
                    event_type = event_type | table_element.Type;
                    my_event_type = my_event_type | table_element.Type;
                    break;
                case CheckState.Indeterminate:
                    event_type = event_type | table_element.Type;
                    break;
                case CheckState.Unchecked:
                    break;
                }
            }
            eventnotifyflag = event_type;
            isMyeventnotifyflag = my_event_type;
        }


        private void ApplyEventNotifyFlag(bool rootEnabled, MyCommon.EVENTTYPE eventnotifyflag, MyCommon.EVENTTYPE isMyeventnotifyflag)
        {
            MyCommon.EVENTTYPE event_type = eventnotifyflag;
            MyCommon.EVENTTYPE my_event_type = isMyeventnotifyflag;

            CheckEventNotify.Checked = rootEnabled;

            foreach ( EventCheckboxTableElement table_element in GetEventCheckboxTable() ) {
                if ( (event_type & table_element.Type) != 0 ) {
                    if ( (my_event_type & table_element.Type) != 0 ) {
                        table_element.CheckBox.CheckState = CheckState.Checked;
                    } else {
                        table_element.CheckBox.CheckState = CheckState.Indeterminate;
                    }
                } else {
                    table_element.CheckBox.CheckState = CheckState.Unchecked;
                }
                table_element.CheckBox.Enabled = rootEnabled;
            }

        }


        private void CheckEventNotify_CheckedChanged(object sender, EventArgs e)
        {
            foreach ( EventCheckboxTableElement table_element in GetEventCheckboxTable() ) {
                table_element.CheckBox.Enabled = CheckEventNotify.Checked;
            }
        }

        //private void CheckForceEventNotify_CheckedChanged(object sender, EventArgs e)
        //{
        //    _MyForceEventNotify = CheckEventNotify.Checked;
        //}

        //private void CheckFavEventUnread_CheckedChanged(object sender, EventArgs e)
        //{
        //    _MyFavEventUnread = CheckFavEventUnread.Checked;
        //}

        //private void ComboBoxTranslateLanguage_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    _MyTranslateLanguage = (new Google()).GetLanguageEnumFromIndex(ComboBoxTranslateLanguage.SelectedIndex);
        //}

        private void SoundFileListup()
        {
            if ( EventSoundFile == null )
                EventSoundFile = string.Empty;
            
            ComboBoxEventNotifySound.Items.Clear();
            ComboBoxEventNotifySound.Items.Add( string.Empty );
            
            DirectoryInfo directory = new DirectoryInfo (Application.StartupPath + Path.DirectorySeparatorChar);            
            if ( Directory.Exists( Path.Combine( Application.StartupPath, "Sounds" ) ) ) {
                directory = directory.GetDirectories( "Sounds" ) [0];
            }
            
            foreach ( FileInfo file in directory.GetFiles("*.wav") ) {
                ComboBoxEventNotifySound.Items.Add( file.Name );
            }
            
            int index = ComboBoxEventNotifySound.Items.IndexOf( EventSoundFile );
            if ( index == -1 )
                index = 0;
            ComboBoxEventNotifySound.SelectedIndex = index;
        }

        //private void ComboBoxEventNotifySound_VisibleChanged(object sender, EventArgs e)
        //{
        //    SoundFileListup();
        //}

        //private void ComboBoxEventNotifySound_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //   if (_soundfileListup) return;

        //    _MyEventSoundFile = (string)ComboBoxEventNotifySound.SelectedItem;
        //}

        private void UserAppointUrlText_Validating(object sender, CancelEventArgs e)
        {
            if ( !UserAppointUrlText.Text.StartsWith( "http" ) && !string.IsNullOrEmpty( UserAppointUrlText.Text ) ) {
                MessageBox.Show( "Text Error:正しいURLではありません" );
            }
        }


        private void IsPreviewFoursquareCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            FoursquareGroupBox.Enabled = IsPreviewFoursquareCheckBox.Checked;
        }


        private void OpenUrl(string url)
        {
            string my_path = url;
            string path = this.BrowserPathText.Text;
            try {
                if ( !string.IsNullOrEmpty( BrowserPath ) ) {
                    if ( path.StartsWith( "\"" ) && path.Length > 2 && path.IndexOf( "\"", 2 ) > -1 ) {
                        int separator = path.IndexOf( "\"", 2 );
                        string browser_path = path.Substring( 1, separator - 1 );
                        string arg = string.Empty;
                        
                        if ( separator < path.Length - 1 ) {
                            arg = path.Substring( separator + 1 );
                        }
                        
                        my_path = arg + " " + my_path;
                        System.Diagnostics.Process.Start( browser_path, my_path );
                    } else {
                        System.Diagnostics.Process.Start( path, my_path );
                    }
                } else {
                    System.Diagnostics.Process.Start( my_path );
                }
            } catch ( Exception ) {
//              MessageBox.Show("ブラウザの起動に失敗、またはタイムアウトしました。" + ex.ToString());
            }
        }


        private void CreateAccountButton_Click(object sender, EventArgs e)
        {
            this.OpenUrl( "https://twitter.com/signup" );
        }


        private void CheckAutoConvertUrl_CheckedChanged(object sender, EventArgs e)
        {
            ShortenTcoCheck.Enabled = CheckAutoConvertUrl.Checked;
        }


        public AppendSettingDialog()
        {
            InitializeComponent();

            this.Icon = Properties.Resources.MIcon;
        }
    }
}
