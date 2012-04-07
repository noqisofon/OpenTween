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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Drawing;


///<summary>
///HttpWebRequest,HttpWebResponseを使用した基本的な通信機能を提供する
///</summary>
///<remarks>
///プロキシ情報などを設定するため、使用前に静的メソッドInitializeConnectionを呼び出すこと。
///通信方式によって必要になるHTTPヘッダの付加などは、派生クラスで行う。
///</remarks>
namespace OpenTween
{


    public class HttpConnection
    {
        ///<summary>
        ///プロキシ
        ///</summary>
        private static WebProxy __proxy = null;

        ///<summary>
        ///ユーザーが選択したプロキシの方式
        ///</summary>
        private static ProxyType __proxy_kind = ProxyType.IE;

        ///<summary>
        ///クッキー保存用コンテナ
        ///</summary>
        private static CookieContainer __cookie_container = new CookieContainer ();

        ///<summary>
        ///初期化済みフラグ
        ///</summary>
        private static bool __is_initialize = false;

        public enum ProxyType
        {
            None,
            IE,
            Specified,
        }

        protected const string PostMethod = "POST";
        protected const string GetMethod = "GET";
        protected const string HeadMethod = "HEAD";

        ///<summary>
        ///HttpWebRequestオブジェクトを取得する。パラメータはGET/HEAD/DELETEではクエリに、POST/PUTではエンティティボディに変換される。
        ///</summary>
        ///<remarks>
        ///追加で必要となるHTTPヘッダや通信オプションは呼び出し元で付加すること
        ///（Timeout,AutomaticDecompression,AllowAutoRedirect,UserAgent,ContentType,Accept,HttpRequestHeader.Authorization,カスタムヘッダ）
        ///POST/PUTでクエリが必要な場合は、requestUriに含めること。
        ///</remarks>
        ///<param name="method">HTTP通信メソッド（GET/HEAD/POST/PUT/DELETE）</param>
        ///<param name="requestUri">通信先URI</param>
        ///<param name="param">GET時のクエリ、またはPOST時のエンティティボディ</param>
        ///<param name="withCookie">通信にcookieを使用するか</param>
        ///<returns>引数で指定された内容を反映したHttpWebRequestオブジェクト</returns>
        protected HttpWebRequest CreateRequest(string method,
                                               Uri requestUri,
                                               IDictionary<string, string> param,
                                               bool withCookie)
        {
            if ( !__is_initialize )
                throw new Exception ("Sequence error.(not initialized)");

            //GETメソッドの場合はクエリとurlを結合
            UriBuilder uri_builder = new UriBuilder (requestUri.AbsoluteUri);
            if ( param != null && (method == "GET" || method == "DELETE" || method == "HEAD") ) {
                uri_builder.Query = CreateQueryString( param );
            }

            HttpWebRequest web_request = (HttpWebRequest)WebRequest.Create( uri_builder.Uri );

            web_request.ReadWriteTimeout = 90 * 1000; //Streamの読み込みは90秒でタイムアウト（デフォルト5分）

            //プロキシ設定
            if ( __proxy_kind != ProxyType.IE )
                web_request.Proxy = __proxy;

            web_request.Method = method;
            if ( method == "POST" || method == "PUT" ) {
                web_request.ContentType = "application/x-www-form-urlencoded";
                //POST/PUTメソッドの場合は、ボディデータとしてクエリ構成して書き込み
                using (StreamWriter writer = new StreamWriter(web_request.GetRequestStream())) {
                    writer.Write( CreateQueryString( param ) );
                }
            }
            //cookie設定
            if ( withCookie )
                web_request.CookieContainer = __cookie_container;
            //タイムアウト設定
            if ( InstanceTimeout > 0 ) {
                web_request.Timeout = InstanceTimeout;
            } else {
                web_request.Timeout = DefaultTimeout;
            }

            return web_request;
        }

        ///<summary>
        ///HttpWebRequestオブジェクトを取得する。multipartでのバイナリアップロード用。
        ///</summary>
        ///<remarks>
        ///methodにはPOST/PUTのみ指定可能
        ///</remarks>
        ///<param name="method">HTTP通信メソッド（POST/PUT）</param>
        ///<param name="requestUri">通信先URI</param>
        ///<param name="param">form-dataで指定する名前と文字列のディクショナリ</param>
        ///<param name="param">form-dataで指定する名前とバイナリファイル情報のリスト</param>
        ///<param name="withCookie">通信にcookieを使用するか</param>
        ///<returns>引数で指定された内容を反映したHttpWebRequestオブジェクト</returns>
        protected HttpWebRequest CreateRequest(string method,
                                               Uri requestUri,
                                               IDictionary<string, string> param,
                                               IList<KeyValuePair<String, FileInfo>> binaryFileInfo,
                                               bool withCookie)
        {
            if ( !__is_initialize )
                throw new Exception ("Sequence error.(not initialized)");

            //methodはPOST,PUTのみ許可
            UriBuilder uri_builder = new UriBuilder (requestUri.AbsoluteUri);
            if ( method == "GET" || method == "DELETE" || method == "HEAD" )
                throw new ArgumentException ("Method must be POST or PUT");
            if ( (param == null || param.Count == 0) && (binaryFileInfo == null || binaryFileInfo.Count == 0) )
                throw new ArgumentException ("Data is empty");

            HttpWebRequest web_request = (HttpWebRequest)WebRequest.Create( uri_builder.Uri );

            //プロキシ設定
            if ( __proxy_kind != ProxyType.IE )
                web_request.Proxy = __proxy;

            web_request.Method = method;
            if ( method == "POST" || method == "PUT" ) {
                string boundary = System.Environment.TickCount.ToString();
                
                web_request.ContentType = "multipart/form-data; boundary=" + boundary;
                using (Stream request_stream = web_request.GetRequestStream()) {
                    //POST送信する文字データを作成
                    if ( param != null ) {
                        string post_data = string.Empty;
                        foreach ( KeyValuePair<string, string> kvp in param ) {
                            post_data += "--" + boundary + "\r\n" +
                                    "Content-Disposition: form-data; name=\"" + kvp.Key + "\"" +
                                    "\r\n\r\n" + kvp.Value + "\r\n";
                        }
                        byte[] post_bytes = Encoding.UTF8.GetBytes( post_data );
                        request_stream.Write( post_bytes, 0, post_bytes.Length );
                    }
                    //POST送信するバイナリデータを作成
                    if ( binaryFileInfo != null ) {
                        foreach ( KeyValuePair<string, FileInfo> pair in binaryFileInfo ) {
                            string post_data = string.Empty;
                            byte[] crlf_bytes = Encoding.UTF8.GetBytes( "\r\n" );
                            //コンテンツタイプの指定
                            string mime = string.Empty;
                            switch ( pair.Value.Extension.ToLower() ) {
                            case ".jpg":
                            case ".jpeg":
                            case ".jpe":
                                mime = "image/jpeg";
                                break;
                            case ".gif":
                                mime = "image/gif";
                                break;
                            case ".png":
                                mime = "image/png";
                                break;
                            case ".tiff":
                            case ".tif":
                                mime = "image/tiff";
                                break;
                            case ".bmp":
                                mime = "image/x-bmp";
                                break;
                            case ".avi":
                                mime = "video/avi";
                                break;
                            case ".wmv":
                                mime = "video/x-ms-wmv";
                                break;
                            case ".flv":
                                mime = "video/x-flv";
                                break;
                            case ".m4v":
                                mime = "video/x-m4v";
                                break;
                            case ".mov":
                                mime = "video/quicktime";
                                break;
                            case ".mp4":
                                mime = "video/3gpp";
                                break;
                            case ".rm":
                                mime = "application/vnd.rn-realmedia";
                                break;
                            case ".mpeg":
                            case ".mpg":
                                mime = "video/mpeg";
                                break;
                            case ".3gp":
                                mime = "movie/3gp";
                                break;
                            case ".3g2":
                                mime = "video/3gpp2";
                                break;
                            default:
                                mime = "application/octet-stream\r\nContent-Transfer-Encoding: binary";
                                break;
                            }
                            post_data = "--" + boundary + "\r\n" +
                                "Content-Disposition: form-data; name=\"" + pair.Key + "\"; filename=\"" +
                                pair.Value.Name + "\"\r\n" +
                                "Content-Type: " + mime + "\r\n\r\n";
                            byte[] post_bytes = Encoding.UTF8.GetBytes( post_data );
                            request_stream.Write( post_bytes, 0, post_bytes.Length );
                            //ファイルを読み出してHTTPのストリームに書き込み
                            using (FileStream file_stream = new FileStream(pair.Value.FullName, FileMode.Open, FileAccess.Read)) {
                                int read_size = 0;
                                byte[] read_bytes = new byte[0x1000];
                                while ( true ) {
                                    read_size = file_stream.Read( read_bytes, 0, read_bytes.Length );
                                    if ( read_size == 0 )
                                        break;
                                    request_stream.Write( read_bytes, 0, read_size );
                                }
                            }
                            request_stream.Write( crlf_bytes, 0, crlf_bytes.Length );
                        }
                    }
                    //終端
                    byte[] end_bytes = Encoding.UTF8.GetBytes( "--" + boundary + "--\r\n" );
                    request_stream.Write( end_bytes, 0, end_bytes.Length );
                }
            }
            //cookie設定
            if ( withCookie )
                web_request.CookieContainer = __cookie_container;
            //タイムアウト設定
            if ( InstanceTimeout > 0 )
                web_request.Timeout = InstanceTimeout;
            else
                web_request.Timeout = DefaultTimeout;

            return web_request;
        }

        ///<summary>
        ///HTTPの応答を処理し、引数で指定されたストリームに書き込み
        ///</summary>
        ///<remarks>
        ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
        ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
        ///gzipファイルのダウンロードを想定しているため、他形式の場合は伸張時に問題が発生する可能性があります。
        ///</remarks>
        ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
        ///<param name="contentStream">[OUT]HTTP応答のボディストリームのコピー先</param>
        ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
        ///<param name="withCookie">通信にcookieを使用する</param>
        ///<returns>HTTP応答のステータスコード</returns>
        protected HttpStatusCode GetResponse(HttpWebRequest webRequest,
                                             Stream contentStream,
                                             IDictionary<string, string> headerInfo,
                                             bool withCookie)
        {
            try {
                using (HttpWebResponse web_response = (HttpWebResponse)webRequest.GetResponse()) {
                    HttpStatusCode status_code = web_response.StatusCode;
                    //cookie保持
                    if ( withCookie )
                        SaveCookie( web_response.Cookies );
                    //リダイレクト応答の場合は、リダイレクト先を設定
                    GetHeaderInfo( web_response, headerInfo );
                    //応答のストリームをコピーして戻す
                    if ( web_response.ContentLength > 0 ) {
                        //gzipなら応答ストリームの内容は伸張済み。それ以外なら伸張する。
                        if ( web_response.ContentEncoding == "gzip" || web_response.ContentEncoding == "deflate" ) {
                            using (Stream stream = web_response.GetResponseStream()) {
                                if ( stream != null )
                                    CopyStream( stream, contentStream );
                            }
                        } else {
                            using (Stream stream = new GZipStream(web_response.GetResponseStream(), CompressionMode.Decompress)) {
                                if ( stream != null )
                                    CopyStream( stream, contentStream );
                            }
                        }
                    }
                    return status_code;
                }
            } catch ( WebException we ) {
                if ( we.Status == WebExceptionStatus.ProtocolError ) {
                    HttpWebResponse res = (HttpWebResponse)we.Response;
                    GetHeaderInfo( res, headerInfo );
                    return res.StatusCode;
                }
                throw;
            }
        }

        ///<summary>
        ///HTTPの応答を処理し、応答ボディデータをテキストとして返却する
        ///</summary>
        ///<remarks>
        ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
        ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
        ///テキストの文字コードはUTF-8を前提として、エンコードはしていません
        ///</remarks>
        ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
        ///<param name="contentText">[OUT]HTTP応答のボディデータ</param>
        ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
        ///<param name="withCookie">通信にcookieを使用する</param>
        ///<returns>HTTP応答のステータスコード</returns>
        protected HttpStatusCode GetResponse(HttpWebRequest webRequest,
                                             out string contentText,
                                             IDictionary<string, string> headerInfo,
                                             bool withCookie)
        {
            try {
                using (HttpWebResponse web_response = (HttpWebResponse)webRequest.GetResponse()) {
                    HttpStatusCode status_code = web_response.StatusCode;
                    //cookie保持
                    if ( withCookie )
                        SaveCookie( web_response.Cookies );
                    //リダイレクト応答の場合は、リダイレクト先を設定
                    GetHeaderInfo( web_response, headerInfo );
                    //応答のストリームをテキストに書き出し
                    using (StreamReader reader = new StreamReader(web_response.GetResponseStream())) {
                        contentText = reader.ReadToEnd();
                    }
                    return status_code;
                }
            } catch ( WebException we ) {
                if ( we.Status == WebExceptionStatus.ProtocolError ) {
                    HttpWebResponse response = (HttpWebResponse)we.Response;
                    GetHeaderInfo( response, headerInfo );
                    using (StreamReader reader = new StreamReader(response.GetResponseStream())) {
                        contentText = reader.ReadToEnd();
                    }
                    return response.StatusCode;
                }
                throw;
            }
        }

        ///<summary>
        ///HTTPの応答を処理します。応答ボディデータが不要な用途向け。
        ///</summary>
        ///<remarks>
        ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
        ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
        ///</remarks>
        ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
        ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
        ///<param name="withCookie">通信にcookieを使用する</param>
        ///<returns>HTTP応答のステータスコード</returns>
        protected HttpStatusCode GetResponse(HttpWebRequest webRequest,
                                             IDictionary<string, string> headerInfo,
                                             bool withCookie)
        {
            try {
                using (HttpWebResponse web_response = (HttpWebResponse)webRequest.GetResponse()) {
                    HttpStatusCode status_code = web_response.StatusCode;
                    //cookie保持
                    if ( withCookie )
                        SaveCookie( web_response.Cookies );
                    //リダイレクト応答の場合は、リダイレクト先を設定
                    GetHeaderInfo( web_response, headerInfo );
                    
                    return status_code;
                }
            } catch ( WebException we ) {
                if ( we.Status == WebExceptionStatus.ProtocolError ) {
                    HttpWebResponse response = (HttpWebResponse)we.Response;
                    GetHeaderInfo( response, headerInfo );
                    
                    return response.StatusCode;
                }
                throw;
            }
        }

        ///<summary>
        ///HTTPの応答を処理し、応答ボディデータをBitmapとして返却します
        ///</summary>
        ///<remarks>
        ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
        ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
        ///</remarks>
        ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
        ///<param name="contentText">[OUT]HTTP応答のボディデータを書き込むBitmap</param>
        ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
        ///<param name="withCookie">通信にcookieを使用する</param>
        ///<returns>HTTP応答のステータスコード</returns>
        protected HttpStatusCode GetResponse(HttpWebRequest webRequest,
                                             out Bitmap contentBitmap,
                                             IDictionary<string, string> headerInfo,
                                             bool withCookie)
        {
            try {
                using (HttpWebResponse web_response = (HttpWebResponse)webRequest.GetResponse()) {
                    HttpStatusCode status_code = web_response.StatusCode;
                    //cookie保持
                    if ( withCookie )
                        SaveCookie( web_response.Cookies );
                    //リダイレクト応答の場合は、リダイレクト先を設定
                    GetHeaderInfo( web_response, headerInfo );
                    //応答のストリームをBitmapにして戻す
                    //if (webRes.ContentLength > 0) contentBitmap = new Bitmap(webRes.GetResponseStream());
                    contentBitmap = new Bitmap (web_response.GetResponseStream());
                    
                    return status_code;
                }
            } catch ( WebException we ) {
                if ( we.Status == WebExceptionStatus.ProtocolError ) {
                    HttpWebResponse response = (HttpWebResponse)we.Response;
                    GetHeaderInfo( response, headerInfo );
                    contentBitmap = null;
                    
                    return response.StatusCode;
                }
                throw;
            }
        }

        ///<summary>
        ///クッキーを保存。ホスト名なしのドメインの場合、ドメイン名から先頭のドットを除去して追加しないと再利用されないため
        ///</summary>
        private void SaveCookie(CookieCollection cookieCollection)
        {
            foreach ( Cookie cokkie in cookieCollection ) {
                if ( cokkie.Domain.StartsWith( "." ) ) {
                    cokkie.Domain = cokkie.Domain.Substring( 1, cokkie.Domain.Length - 1 );
                    __cookie_container.Add( cokkie );
                }
            }
        }

        ///<summary>
        ///in/outのストリームインスタンスを受け取り、コピーして返却
        ///</summary>
        ///<param name="inStream">コピー元ストリームインスタンス。読み取り可であること</param>
        ///<param name="outStream">コピー先ストリームインスタンス。書き込み可であること</param>
        private void CopyStream(Stream inStream, Stream outStream)
        {
            if ( inStream == null )
                throw new ArgumentNullException ("inStream");
            if ( outStream == null )
                throw new ArgumentNullException ("outStream");
            if ( !inStream.CanRead )
                throw new ArgumentException ("Input stream can not read.");
            if ( !outStream.CanWrite )
                throw new ArgumentException ("Output stream can not write.");
            if ( inStream.CanSeek && inStream.Length == 0 )
                throw new ArgumentException ("Input stream do not have data.");

            do {
                byte[] buffer = new byte[1024];
                int i = buffer.Length;
                i = inStream.Read( buffer, 0, i );
                if ( i == 0 )
                    break;
                outStream.Write( buffer, 0, i );
            } while (true);
        }

        ///<summary>
        ///headerInfoのキー情報で指定されたHTTPヘッダ情報を取得・格納する。redirect応答時はLocationヘッダの内容を追記する
        ///</summary>
        ///<param name="webResponse">HTTP応答</param>
        ///<param name="headerInfo">[IN/OUT]キーにヘッダ名を指定したデータ空のコレクション。取得した値をデータにセットして戻す</param>
        private void GetHeaderInfo(HttpWebResponse webResponse,
                                   IDictionary<string, string> headerInfo)
        {
            if ( headerInfo == null )
                return;

            if ( headerInfo.Count > 0 ) {
                string[] keys = new string[headerInfo.Count];
                headerInfo.Keys.CopyTo( keys, 0 );
                foreach ( string key in keys ) {
                    if ( Array.IndexOf( webResponse.Headers.AllKeys, key ) > -1 ) {
                        headerInfo [key] = webResponse.Headers [key];
                    } else {
                        headerInfo [key] = "";
                    }
                }
            }

            HttpStatusCode status_code = webResponse.StatusCode;
            if ( status_code == HttpStatusCode.MovedPermanently ||
                status_code == HttpStatusCode.Found ||
                status_code == HttpStatusCode.SeeOther ||
                status_code == HttpStatusCode.TemporaryRedirect ) {
                if ( headerInfo.ContainsKey( "Location" ) ) {
                    headerInfo ["Location"] = webResponse.Headers ["Location"];
                } else {
                    headerInfo.Add( "Location", webResponse.Headers ["Location"] );
                }
            }
        }

        ///<summary>
        ///クエリコレクションをkey=value形式の文字列に構成して戻す
        ///</summary>
        ///<param name="param">クエリ、またはポストデータとなるkey-valueコレクション</param>
        protected string CreateQueryString(IDictionary<string, string> param)
        {
            if ( param == null || param.Count == 0 )
                return string.Empty;

            StringBuilder query_string_builder = new StringBuilder ();
            foreach ( string key in param.Keys ) {
                query_string_builder.AppendFormat( "{0}={1}&", UrlEncode( key ), UrlEncode( param [key] ) );
            }
            return query_string_builder.ToString( 0, query_string_builder.Length - 1 );
        }

        ///<summary>
        ///クエリ形式（key1=value1&key2=value2&...）の文字列をkey-valueコレクションに詰め直し
        ///</summary>
        ///<param name="queryString">クエリ文字列</param>
        ///<returns>key-valueのコレクション</returns>
        protected NameValueCollection ParseQueryString(string queryString)
        {
            NameValueCollection query = new NameValueCollection ();
            string[] parts = queryString.Split( '&' );
            foreach ( string part in parts ) {
                int index = part.IndexOf( '=' );
                if ( index == -1 )
                    query.Add( Uri.UnescapeDataString( part ), "" );
                else
                    query.Add( Uri.UnescapeDataString( part.Substring( 0, index ) ), Uri.UnescapeDataString( part.Substring( index + 1 ) ) );
            }
            return query;
        }

        ///<summary>
        ///2バイト文字も考慮したUrlエンコード
        ///</summary>
        ///<param name="str">エンコードする文字列</param>
        ///<returns>エンコード結果文字列</returns>
        protected string UrlEncode(string stringToEncode)
        {
            const string UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
            StringBuilder encoded_text_builder = new StringBuilder ();
            byte[] bytes = Encoding.UTF8.GetBytes( stringToEncode );

            foreach ( byte b in bytes ) {
                if ( UnreservedChars.IndexOf( (char)b ) != -1 )
                    encoded_text_builder.Append( (char)b );
                else
                    encoded_text_builder.AppendFormat( "%{0:X2}", b );
            }
            return encoded_text_builder.ToString();
        }

        #region "InstanceTimeout"
        ///<summary>
        ///通信タイムアウト時間（ms）
        ///</summary>
        private int timeout_ = 0;

        ///<summary>
        ///通信タイムアウト時間（ms）。10～120秒の範囲で指定。範囲外は20秒とする
        ///</summary>
        protected int InstanceTimeout {
            get { return timeout_; }
            set {
                const int TimeoutMinValue = 10000;
                const int TimeoutMaxValue = 120000;
                if ( value < TimeoutMinValue || value > TimeoutMaxValue )
                    throw new ArgumentOutOfRangeException ("Set " + TimeoutMinValue + "-" + TimeoutMaxValue + ": Value=" + value);
                else
                    timeout_ = value;
            }
        }
        #endregion

        #region "DefaultTimeout"
        ///<summary>
        ///通信タイムアウト時間（ms）
        ///</summary>
        private static int __timeout = 20000;

        ///<summary>
        ///通信タイムアウト時間（ms）。10～120秒の範囲で指定。範囲外は20秒とする
        ///</summary>
        protected static int DefaultTimeout {
            get { return __timeout; }
            set {
                const int TimeoutMinValue = 10000;
                const int TimeoutMaxValue = 120000;
                const int TimeoutDefaultValue = 20000;
                if ( value < TimeoutMinValue || value > TimeoutMaxValue )
                    // 範囲外ならデフォルト値設定
                    __timeout = TimeoutDefaultValue;
                else
                    __timeout = value;
            }
        }
        #endregion

        ///<summary>
        ///通信クラスの初期化処理。タイムアウト値とプロキシを設定する
        ///</summary>
        ///<remarks>
        ///通信開始前に最低一度呼び出すこと
        ///</remarks>
        ///<param name="timeout">タイムアウト値（秒）</param>
        ///<param name="proxyType">なし・指定・IEデフォルト</param>
        ///<param name="proxyAddress">プロキシのホスト名orIPアドレス</param>
        ///<param name="proxyPort">プロキシのポート番号</param>
        ///<param name="proxyUser">プロキシ認証が必要な場合のユーザ名。不要なら空文字</param>
        ///<param name="proxyPassword">プロキシ認証が必要な場合のパスワード。不要なら空文字</param>
        public static void InitializeConnection(
                int timeout,
                ProxyType proxyType,
                string proxyAddress,
                int proxyPort,
                string proxyUser,
                string proxyPassword)
        {
            __is_initialize = true;
            ServicePointManager.Expect100Continue = false;
            DefaultTimeout = timeout * 1000;     //s -> ms
            switch ( proxyType ) {
            case ProxyType.None:
                __proxy = null;
                break;
            case ProxyType.Specified:
                __proxy = new WebProxy ("http://" + proxyAddress + ":" + proxyPort);
                if ( !String.IsNullOrEmpty( proxyUser ) || !String.IsNullOrEmpty( proxyPassword ) )
                    __proxy.Credentials = new NetworkCredential (proxyUser, proxyPassword);
                break;
            case ProxyType.IE:
                    //IE設定（システム設定）はデフォルト値なので処理しない
                break;
            }
            __proxy_kind = proxyType;

            Win32Api.SetProxy( proxyType, proxyAddress, proxyPort, proxyUser, proxyPassword );
        }
    }
}