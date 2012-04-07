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
// under the terms of the GNU General public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
// 
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General public License
// for more details. 
// 
// You should have received a copy of the GNU General public License along
// with this program. if (not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;


namespace OpenTween
{


    public class HttpTwitter : ICloneable
    {
        //OAuth関連
        ///<summary>
        ///OAuthのアクセストークン取得先URI
        ///</summary>
        private const string AccessTokenUrlXAuth = "https://api.twitter.com/oauth/access_token";
        private const string RequestTokenUrl = "https://api.twitter.com/oauth/request_token";
        private const string AuthorizeUrl = "https://api.twitter.com/oauth/authorize";
        private const string AccessTokenUrl = "https://api.twitter.com/oauth/access_token";
        private static string __protocol = "http://";
        private const string PostMethod = "POST";
        private const string GetMethod = "GET";
        private IHttpConnection http_connection_; //HttpConnectionApi or HttpConnectionOAuth
        private HttpVarious http_various_ = new HttpVarious ();

        private enum AuthMethod
        {
            OAuth,
                Basic,
                }
        private AuthMethod connection_type_ = AuthMethod.Basic;
        private string request_token_;
        private static string __token = string.Empty;
        private static string __token_secret = string.Empty;
        private static string __user_name = string.Empty;


        public void Initialize(string accessToken,
                               string accessTokenSecret,
                               string username,
                               long userId)
        {
            //for OAuth
            HttpOAuthApiProxy oauth_api_proxy = new HttpOAuthApiProxy ();
            if ( __token != accessToken || __token_secret != accessTokenSecret ||
                 __user_name != username || connection_type_ != AuthMethod.OAuth ) {
                // 以前の認証状態よりひとつでも変化があったらhttpヘッダより読み取ったカウントは初期化
                __token = accessToken;
                __token_secret = accessTokenSecret;
                __user_name = username;
            }
            oauth_api_proxy.Initialize( ApplicationSettings.TwitterConsumerKey,
                                        ApplicationSettings.TwitterConsumerSecret,
                                        accessToken,
                                        accessTokenSecret,
                                        username,
                                        userId,
                                        "screen_name",
                                        "user_id" );
            http_connection_ = oauth_api_proxy;
            connection_type_ = AuthMethod.OAuth;
            request_token_ = string.Empty;
        }


        public string AccessToken {
            get {
                if ( http_connection_ != null )
                    return ((HttpConnectionOAuth)http_connection_).AccessToken;
                else
                    return string.Empty;
            }
        }


        public string AccessTokenSecret {
            get {
                if ( http_connection_ != null )
                    return ((HttpConnectionOAuth)http_connection_).AccessTokenSecret;
                else
                    return string.Empty;
            }
        }


        public string AuthenticatedUsername {
            get {
                if ( http_connection_ != null )
                    return http_connection_.AuthUsername;
                else
                    return string.Empty;
            }
        }


        public long AuthenticatedUserId {
            get {
                if ( http_connection_ != null )
                    return http_connection_.AuthUserId;
                else
                    return 0;
            }
            set {
                if ( http_connection_ != null )
                    http_connection_.AuthUserId = value;
            }
        }


        public string Password {
            get {
                return string.Empty;
            }
        }


        public bool AuthGetRequestToken(ref string content)
        {
            Uri auth_uri = null;
            bool result = ((HttpOAuthApiProxy)http_connection_).AuthenticatePinFlowRequest( RequestTokenUrl,
                                                                                            AuthorizeUrl,
                                                                                            ref request_token_,
                                                                                            ref auth_uri );
            
            content = auth_uri.ToString();
            
            return result;
        }


        public HttpStatusCode AuthGetAccessToken(string pin)
        {
            return ((HttpOAuthApiProxy)http_connection_).AuthenticatePinFlow( AccessTokenUrl, request_token_, pin );
        }


        public HttpStatusCode AuthUserAndPass(string username, string password, ref string content)
        {
            return http_connection_.Authenticate( new Uri (AccessTokenUrlXAuth), username, password, ref content );
        }


        public void ClearAuthInfo()
        {
            this.Initialize( string.Empty, string.Empty, string.Empty, 0 );
        }


        public static bool UseSsl {
            set {
                if ( value )
                    __protocol = "https://";
                else
                    __protocol = "http://";
            }
        }


        public HttpStatusCode UpdateStatus(string status, long replyToId, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();
            
            param.Add( "status", status );
            if ( replyToId > 0 )
                param.Add( "in_reply_to_status_id", replyToId.ToString() );
            
            param.Add( "include_entities", "true" );
            //if (AppendSettingDialog.Instance.ShortenTco && AppendSettingDialog.Instance.UrlConvertAuto) param.Add("wrap_links", "true")

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/statuses/update.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode UpdateStatusWithMedia(string status, long replyToId, FileInfo mediaFile, ref string content)
        {
            //画像投稿用エンドポイント
            IDictionary<string, string> param = new Dictionary<string, string> ();
            
            param.Add( "status", status );
            if ( replyToId > 0 )
                param.Add( "in_reply_to_status_id", replyToId.ToString() );
            
            param.Add( "include_entities", "true" );
            //if (AppendSettingDialog.Instance.ShortenTco && AppendSettingDialog.Instance.UrlConvertAuto) param.Add("wrap_links", "true")

            IList<KeyValuePair<string, FileInfo>> binary = new List<KeyValuePair<string, FileInfo>> ();
            binary.Add( new KeyValuePair<string, FileInfo> ("media[]", mediaFile) );

            return http_connection_.GetContent( PostMethod,
                                                new Uri ("https://upload.twitter.com/1/statuses/update_with_media.json"),
                                                param,
                                                binary,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode DestroyStatus(long id)
        {
            string content = null;

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/statuses/destroy/" + id.ToString() + ".json" ),
                                                null,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode SendDirectMessage(string status, string sendto, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();
            
            param.Add( "text", status );
            param.Add( "screen_name", sendto );
            //if (AppendSettingDialog.Instance.ShortenTco && AppendSettingDialog.Instance.UrlConvertAuto) param.Add("wrap_links", "true")

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/direct_messages/new.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode DestroyDirectMessage(long id)
        {
            string content = null;

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/direct_messages/destroy/" + id.ToString() + ".json" ),
                                                null,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode RetweetStatus(long id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/statuses/retweet/" + id.ToString() + ".json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode ShowUserInfo(string screenName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();
            
            param.Add( "screen_name", screenName );
            param.Add( "include_entities", "true" );
            
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/users/show.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode CreateFriendships(string screenName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", screenName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/friendships/create.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode DestroyFriendships(string screenName, ref string content)
        {
            Dictionary<string, string> param = new Dictionary<string, string> ();
            param.Add( "screen_name", screenName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/friendships/destroy.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode CreateBlock(string screenName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", screenName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/blocks/create.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode DestroyBlock(string screenName, ref string content)
        {
            Dictionary<string, string> param = new Dictionary<string, string> ();
            param.Add( "screen_name", screenName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/blocks/destroy.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode ReportSpam(string screenName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", screenName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/report_spam.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode ShowFriendships(string souceScreenName, string targetScreenName, ref string content)
        {
            Dictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "source_screen_name", souceScreenName );
            param.Add( "target_screen_name", targetScreenName );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/friendships/show.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode ShowStatuses(long id, ref string content)
        {
            Dictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/show/" + id.ToString() + ".json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode CreateFavorites(long id, ref string content)
        {
            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/favorites/create/" + id.ToString() + ".json" ),
                                                null,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode DestroyFavorites(long id, ref string content)
        {
            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/favorites/destroy/" + id.ToString() + ".json" ),
                                                null,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode HomeTimeline(int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/home_timeline.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode UserTimeline(long user_id, string screen_name, int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( (user_id == 0 && string.IsNullOrEmpty( screen_name )) ||
                 (user_id != 0 && !string.IsNullOrEmpty( screen_name )) ) {
                return HttpStatusCode.BadRequest;
            }
            if ( user_id > 0 )
                param.Add( "user_id", user_id.ToString() );
            if ( !string.IsNullOrEmpty( screen_name ) )
                param.Add( "screen_name", screen_name );
            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );

            param.Add( "include_rts", "true" );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/user_timeline.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode PublicTimeline(int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );

            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/public_timeline.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode Mentions(int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/mentions.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode DirectMessages(int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/direct_messages.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode DirectMessagesSent(int count, long max_id, long since_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/direct_messages/sent.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode Favorites(int count, int page, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count != 20 )
                param.Add( "count", count.ToString() );

            if ( page > 0 ) {
                param.Add( "page", page.ToString() );
            }
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/favorites.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode PhoenixSearch(string querystr, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();
            string[] tmp;
            string[] paramstr;

            if ( string.IsNullOrEmpty( querystr ) )
                return HttpStatusCode.BadRequest;

            tmp = querystr.Split( new char[] {'?', '&'}, StringSplitOptions.RemoveEmptyEntries );
            foreach ( string tmp2 in tmp ) {
                paramstr = tmp2.Split( new char[] {'='} );
                param.Add( paramstr [0], paramstr [1] );
            }

            return http_various_.GetContent( GetMethod,
                                             CreateTwitterUri( "/phoenix_search.phoenix" ),
                                             param,
                                             out content,
                                             null,
                                             MyCommon.GetAssemblyName() );
        }


        public HttpStatusCode PhoenixSearch(string words, string lang, int rpp, int page, long sinceId, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( !string.IsNullOrEmpty( words ) )
                param.Add( "q", words );
            param.Add( "include_entities", "1" );
            param.Add( "contributor_details", "true" );

            if ( !string.IsNullOrEmpty( lang ) )
                param.Add( "lang", lang );
            if ( rpp > 0 )
                param.Add( "rpp", rpp.ToString() );
            if ( page > 0 )
                param.Add( "page", page.ToString() );
            if ( sinceId > 0 )
                param.Add( "since_id", sinceId.ToString() );

            if ( param.Count == 0 )
                return HttpStatusCode.BadRequest;

            return http_various_.GetContent( GetMethod,
                                             CreateTwitterUri( "/phoenix_search.phoenix" ),
                                             param,
                                             out content,
                                             null,
                                             MyCommon.GetAssemblyName() );
        }


        public HttpStatusCode Search(string words, string lang, int rpp, int page, long sinceId, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( !string.IsNullOrEmpty( words ) )
                param.Add( "q", words );
            if ( !string.IsNullOrEmpty( lang ) )
                param.Add( "lang", lang );
            if ( rpp > 0 )
                param.Add( "rpp", rpp.ToString() );
            if ( page > 0 )
                param.Add( "page", page.ToString() );
            if ( sinceId > 0 )
                param.Add( "since_id", sinceId.ToString() );

            if ( param.Count == 0 )
                return HttpStatusCode.BadRequest;

            return http_various_.GetContent( GetMethod,
                                             CreateTwitterSearchUri( "/search.atom" ),
                                             param,
                                             out content,
                                             null,
                                             MyCommon.GetAssemblyName() );
        }


        public HttpStatusCode SavedSearches(ref string content)
        {
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/saved_searches.json" ),
                                                null,
                                                ref content,
                                                null,
                                                GetApiCallback );
        }


        public HttpStatusCode FollowerIds(long cursor, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "cursor", cursor.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/followers/ids.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode NoRetweetIds(long cursor, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "cursor", cursor.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/friendships/no_retweet_ids.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode RateLimitStatus(ref string content)
        {
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/account/rate_limit_status.json" ),
                                                null,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }

#region "Lists"
        public HttpStatusCode GetLists(string user, long cursor, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", user );
            param.Add( "cursor", cursor.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/lists.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode UpdateListID(string user, string list_id, string name, Boolean isPrivate, string description, ref string content)
        {
            string mode = "public";

            if ( isPrivate )
                mode = "private";

            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", user );
            param.Add( "list_id", list_id );
            if ( name != null )
                param.Add( "name", name );
            if ( mode != null )
                param.Add( "mode", mode );
            if ( description != null )
                param.Add( "description", description );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/lists/update.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode DeleteListID(string user, string list_id, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", user );
            param.Add( "list_id", list_id );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/lists/destroy.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode GetListsSubscriptions(string user, long cursor, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", user );
            param.Add( "cursor", cursor.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/lists/subscriptions.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode GetListsStatuses(long userId, long list_id, int per_page, long max_id, long since_id, Boolean isRTinclude, ref string content)
        {
            // 認証なくても取得できるが、protectedユーザー分が抜ける
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "user_id", userId.ToString() );
            param.Add( "list_id", list_id.ToString() );

            if ( isRTinclude )
                param.Add( "include_rts", "true" );
            if ( per_page > 0 )
                param.Add( "per_page", per_page.ToString() );
            if ( max_id > 0 )
                param.Add( "max_id", max_id.ToString() );
            if ( since_id > 0 )
                param.Add( "since_id", since_id.ToString() );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/lists/statuses.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode CreateLists(string listname, Boolean isPrivate, string description, ref string content)
        {
            string mode = "public";

            if ( isPrivate )
                mode = "private";

            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "name", listname );
            param.Add( "mode", mode );
            if ( !string.IsNullOrEmpty( description ) )
                param.Add( "description", description );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/lists/create.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode GetListMembers(string user, string list_id, long cursor, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", user );
            param.Add( "list_id", list_id );
            param.Add( "cursor", cursor.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/lists/members.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode CreateListMembers(string list_id, string memberName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "list_id", list_id );
            param.Add( "screen_name", memberName );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/lists/members/create.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }

        //public HttpStatusCode CreateListMembers(string user, string list_id, string memberName, ref string content)
        //{
        //    //正常に動かないので旧APIで様子見
        //    //Dictionary<string, string> param = new Dictionary<string, string>();
        //    //param.Add("screen_name", user)
        //    //param.Add("list_id", list_id)
        //    //param.Add("member_screen_name", memberName)
        //    //return httpCon.GetContent(PostMethod,
        //    //                    CreateTwitterUri("/1/lists/members/create.json"),
        //    //                    param,
        //    //                    ref content,
        //    //                    null,
        //    //                    null)
        //    Dictionary<string, string> param = new Dictionary<string, string>();
        //    param.Add("id", memberName)
        //    return httpCon.GetContent(PostMethod,
        //                        CreateTwitterUri("/1/" + user + "/" + list_id + "/members.json"),
        //                        param,
        //                        ref content,
        //                        null,
        //                        null)
        //}

        public HttpStatusCode DeleteListMembers(string list_id, string memberName, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", memberName );
            param.Add( "list_id", list_id );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/lists/members/destroy.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }

        //public HttpStatusCode DeleteListMembers(string user, string list_id, string memberName, ref string content)
        //{
        //    //Dictionary<string, string> param = new Dictionary<string, string>();
        //    //param.Add("screen_name", user)
        //    //param.Add("list_id", list_id)
        //    //param.Add("member_screen_name", memberName)
        //    //return httpCon.GetContent(PostMethod,
        //    //                    CreateTwitterUri("/1/lists/members/destroy.json"),
        //    //                    param,
        //    //                    ref content,
        //    //                    null,
        //    //                    null)
        //    Dictionary<string, string> param = new Dictionary<string, string>();
        //    param.Add("id", memberName)
        //    param.Add("_method", "DELETE")
        //    return httpCon.GetContent(PostMethod,
        //                        CreateTwitterUri("/1/" + user + "/" + list_id + "/members.json"),
        //                        param,
        //                        ref content,
        //                        null,
        //                        null)
        //}

        public HttpStatusCode ShowListMember(string list_id, string memberName, ref string content)
        {
            // 新 API が member_screen_name も member_user_id も無視して、自分のIDを返してくる。
            // 正式にドキュメントに反映されるまで旧 API を使用する
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "screen_name", memberName );
            param.Add( "list_id", list_id );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/lists/members/show.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
            //return httpCon.GetContent(GetMethod,
            //                          CreateTwitterUri("/1/" + user + "/" + list_id + "/members/" + id + ".json"),
            //                          null,
            //                          ref content,
            //                          MyCommon.TwitterApiInfo.HttpHeaders,
            //                          GetApiCallback);
        }
#endregion

        public HttpStatusCode Statusid_retweeted_by_ids(long statusid, int count, int page, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( count > 0 )
                param.Add( "count", count.ToString() );
            if ( page > 0 )
                param.Add( "page", page.ToString() );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/statuses/" + statusid.ToString() + "/retweeted_by/ids.json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode UpdateProfile(string name, string url, string location, string description, ref string content)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "name", name );
            param.Add( "url", url );
            param.Add( "location", location );
            param.Add( "description", description );
            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/account/update_profile.json" ),
                                                param,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode UpdateProfileImage(FileInfo imageFile, ref string content)
        {
            IList<KeyValuePair<string, FileInfo>> binary = new List<KeyValuePair<string, FileInfo>> ();

            binary.Add( new KeyValuePair<string, FileInfo> ("image", imageFile) );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterUri( "/1/account/update_profile_image.json" ),
                                                null,
                                                binary,
                                                ref content,
                                                null,
                                                null );
        }


        public HttpStatusCode GetRelatedResults(long id, ref string content)
        {
            //認証なくても取得できるが、protectedユーザー分が抜ける
            IDictionary<string, string> param = new Dictionary<string, string> ();

            param.Add( "include_entities", "true" );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/related_results/show/" + id.ToString() + ".json" ),
                                                param,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode GetBlockUserIds(ref string content)
        {
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/blocks/blocking/ids.json" ),
                                                null,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode GetConfiguration(ref string content)
        {
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/help/configuration.json" ),
                                                null,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }


        public HttpStatusCode VerifyCredentials(ref string content)
        {
            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUri( "/1/account/verify_credentials.json" ),
                                                null,
                                                ref content,
                                                MyCommon.TwitterApiInfo.HttpHeaders,
                                                GetApiCallback );
        }

#region "Proxy API"
        private static string __twitter_url = "api.twitter.com";
        private static string __twitter_search_url = "search.twitter.com";
        private static string __twitter_user_stream_url = "userstream.twitter.com";
        private static string __twitter_stream_url = "stream.twitter.com";


        private Uri CreateTwitterUri(string path)
        {
            return new Uri (string.Format( "{0}{1}{2}", __protocol, __twitter_url, path ));
        }


        private Uri CreateTwitterSearchUri(string path)
        {
            return new Uri (string.Format( "{0}{1}{2}", __protocol, __twitter_search_url, path ));
        }


        private Uri CreateTwitterUserStreamUri(string path)
        {
            return new Uri (string.Format( "{0}{1}{2}", "https://", __twitter_user_stream_url, path ));
        }


        private Uri CreateTwitterStreamUri(string path)
        {
            return new Uri (string.Format( "{0}{1}{2}", "http://", __twitter_stream_url, path ));
        }


        public static string TwitterUrl {
            set {
                __twitter_url = value;
                HttpOAuthApiProxy.ProxyHost = value;
            }
        }


        public static string TwitterSearchUrl {
            set {
                __twitter_search_url = value;
            }
        }
#endregion

        private void GetApiCallback(Object sender, ref HttpStatusCode code, ref string content)
        {
            if ( code < HttpStatusCode.InternalServerError )
                MyCommon.TwitterApiInfo.ParseHttpHeaders( MyCommon.TwitterApiInfo.HttpHeaders );
        }


        public HttpStatusCode UserStream(ref Stream content,
                                         bool allAtReplies,
                                         string trackwords,
                                         string userAgent)
        {
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( allAtReplies )
                param.Add( "replies", "all" );

            if ( !string.IsNullOrEmpty( trackwords ) )
                param.Add( "track", trackwords );

            return http_connection_.GetContent( GetMethod,
                                                CreateTwitterUserStreamUri( "/2/user.json" ),
                                                param,
                                                ref content,
                                                userAgent );
        }


        public HttpStatusCode FilterStream(ref Stream content,
                                           string trackwords,
                                           string userAgent)
        {
            //文中の日本語キーワードに反応せず、使えない（スペースで分かち書きしてないと反応しない）
            IDictionary<string, string> param = new Dictionary<string, string> ();

            if ( !string.IsNullOrEmpty( trackwords ) )
                param.Add( "track", string.Join( ",", trackwords.Split( " ".ToCharArray() ) ) );

            return http_connection_.GetContent( PostMethod,
                                                CreateTwitterStreamUri( "/1/statuses/filter.json" ),
                                                param,
                                                ref content,
                                                userAgent );
        }


        public void RequestAbort()
        {
            http_connection_.RequestAbort();
        }


        public object Clone()
        {
            HttpTwitter my_copy = new HttpTwitter ();

            my_copy.Initialize( this.AccessToken, this.AccessTokenSecret, this.AuthenticatedUsername, this.AuthenticatedUserId );

            return my_copy;
        }
    }
}
