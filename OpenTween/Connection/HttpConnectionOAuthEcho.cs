// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      spinor (@tplantd) <http://d.hatena.ne.jp/spinor/>
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
using HttpConnectionOAuth = OpenTween.HttpConnectionOAuth;
using Uri = System.Uri;
using HttpWebRequest = System.Net.HttpWebRequest;
using System.Collections.Generic;

// for Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>
using HttpConnection = OpenTween.HttpConnection;
using StringBuilder = System.Text.StringBuilder;


namespace OpenTween
{


    public class HttpConnectionOAuthEcho : HttpConnectionOAuth
    {
        private Uri realm_;
        private Uri service_provider_;


        public Uri Realm {
            set { this.realm_ = value; }
        }


        public Uri ServiceProvider {
            set { this.service_provider_ = value; }
        }


        protected override void AppendOAuthInfo(HttpWebRequest webRequest, IDictionary< string, string > query, string token, string tokenSecret)
        {
            // OAuth共通情報取得
            IDictionary< string, string > parameter = this.GetOAuthParameter( token );
            // OAuth共通情報にquery情報を追加
            if ( query != null )
                foreach ( KeyValuePair< string, string > item in query )
                    parameter.Add( item.Key, item.Value );
            // 署名の作成・追加(GETメソッド固定。ServiceProvider呼び出し用の署名作成)
            parameter.Add( "oauth_signature", this.CreateSignature( tokenSecret, HttpConnection.GetMethod, this.service_provider_, parameter ) );
            // HTTPリクエストのヘッダに追加
            StringBuilder http_header_builder = new StringBuilder ("OAuth ");
            http_header_builder.AppendFormat( "realm=\"{0}://{1}{2}\",", this.realm_.Scheme, this.realm_.Host, this.realm_.AbsolutePath );
            foreach ( KeyValuePair< string, string > item in parameter )
                if ( item.Key.StartsWith( "oauth_" ) )
                    http_header_builder.AppendFormat( "{0}=\"{1}\",", item.Key, this.UrlEncode( item.Value ) );
            webRequest.Headers.Add( "X-Verify-Credentials-Authorization", http_header_builder.ToString() );
            webRequest.Headers.Add( "X-Auth-Service-Provider", string.Format( "{0}://{1}{2}", this.service_provider_.Scheme, this.service_provider_.Host, this.service_provider_.AbsolutePath ) );
        }


        public HttpConnectionOAuthEcho(Uri realm, Uri serviceProvider)
        {
            this.realm_ = realm;
            this.service_provider_ = serviceProvider;
        }
    }
}
