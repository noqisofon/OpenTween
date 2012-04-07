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

using HttpConnectionOAuthEcho = OpenTween.HttpConnectionOAuthEcho;
using IMultimediaShareService = OpenTween.IMultimediaShareService;
using FileInfo = System.IO.FileInfo;
using NotSupportedException = System.NotSupportedException;
using HttpStatusCode = System.Net.HttpStatusCode;
using Exception = System.Exception;
using XmlDocument = System.Xml.XmlDocument;
using XmlException = System.Xml.XmlException;
using ArgumentException = System.ArgumentException;
using System.Collections.Generic; // for Dictionary<TKey, TValue>, List<T>, KeyValuePair<TKey, TValue>
using Uri = System.Uri;
using Array = System.Array;
using UploadFileType = OpenTween.MyCommon.UploadFileType;

namespace OpenTween
{
    public class ImgLyMultimediaShareService : HttpConnectionOAuthEcho, IMultimediaShareService
    {
        private string[] picture_extensions_ = new string[] { ".jpg", ".jpeg", ".gif", ".png" };

        private const long max_filesize_ = 4 * 1024 * 1024;

        private Twitter twitter_;

        public string Upload( ref string filePath, ref string message, long reply_to )
        {
            if ( string.IsNullOrEmpty( filePath ) )
                return "Err:File isn't specified.";
            if ( string.IsNullOrEmpty( message ) )
                message = "";

            FileInfo media_file;
            try
            {
                media_file = new FileInfo( filePath );
            }
            catch ( NotSupportedException ex )
            {
                return "Err:" + ex.Message;
            }
            if ( media_file == null || !media_file.Exists )
                return "Err:File isn't exists.";

            string content = string.Empty;
            HttpStatusCode status_code;
            // img.lyへの投稿
            try
            {
                status_code = this.UploadFile( media_file, message, ref content );
            }
            catch ( Exception ex )
            {
                return "Err:" + ex.Message;
            }

            string url = string.Empty;
            if ( status_code == HttpStatusCode.OK )
                {
                    XmlDocument document = new XmlDocument();
                    try
                    {
                        document.LoadXml( content );
                        // URLの取得
                        url = document.SelectSingleNode( "/image/url" ).InnerText;
                    }
                    catch ( XmlException ex )
                    {
                        return "Err:" + ex.Message;
                    }
                    catch ( Exception ex )
                    {
                        return "Err:" + ex.Message;
                    }
                }
            else
                {
                    return "Err:" + status_code.ToString();
                }
            // アップロードまでは成功
            filePath = "";
            if ( string.IsNullOrEmpty( url ) )
                url = "";
            // Twitterへの投稿
            // 投稿メッセージの再構成
            if ( string.IsNullOrEmpty( message ) )
                message = "";
            if ( message.Length + AppendSettingDialog.Instance.twitter_config_.CharactersReservedPerMedia + 1 > 140 )
                message = message.Substring( 0, 140 - AppendSettingDialog.Instance.twitter_config_.CharactersReservedPerMedia - 1 ) + " " + url;
            else
                message += " " + url;

            return twitter_.PostStatus( message, reply_to );
        }

        private HttpStatusCode UploadFile( FileInfo mediaFile, string message, ref string content )
        {
            // Message必須
            if ( string.IsNullOrEmpty( message ) )
                message = "";
            // Check filetype and size(Max 4MB)
            if ( !this.CheckValidExtension( mediaFile.Extension ) )
                throw new ArgumentException( "Service don't support this filetype." );
            if ( !this.CheckValidFilesize( mediaFile.Extension, mediaFile.Length ) )
                throw new ArgumentException( "File is too large." );

            IDictionary< string, string > param = new Dictionary< string, string >();
            param.Add( "message", message );
            IList< KeyValuePair< string, FileInfo > > binary = new List< KeyValuePair< string, FileInfo > >();
            binary.Add( new KeyValuePair< string, FileInfo >( "media", mediaFile ) );
            this.InstanceTimeout = 60000; // タイムアウト60秒

            return this.GetContent( HttpConnection.PostMethod, new Uri( "http://img.ly/api/2/upload.xml" ), param, binary, ref content, null, null );
        }

        public bool CheckValidExtension( string ext )
        {
            if ( Array.IndexOf( this.picture_extensions_, ext.ToLower() ) > -1 )
                return true;

            return false;
        }

        public string GetFileOpenDialogFilter()
        {
            return "Image Files(*.gif;*.jpg;*.jpeg;*.png)|*.gif;*.jpg;*.jpeg;*.png";
        }

        public UploadFileType GetFileType( string ext )
        {
            if ( this.CheckValidExtension( ext ) )
                return UploadFileType.Picture;

            return UploadFileType.Invalid;
        }

        public bool IsSupportedFileType( UploadFileType type )
        {
            return type.Equals( UploadFileType.Picture );
        }

        public bool CheckValidFilesize( string ext, long fileSize )
        {
            if ( this.CheckValidExtension( ext ) )
                return fileSize <= ImgLyMultimediaShareService.max_filesize_;

            return false;
        }

        public bool Configuration( string key, object value )
        {
            return true;
        }

        public ImgLyMultimediaShareService( Twitter twitter )
        : base( new Uri( "http://api.twitter.com/" ), new Uri( "https://api.twitter.com/1/account/verify_credentials.json" ) )
        {
            this.twitter_ = twitter;
            this.Initialize( ApplicationSettings.TwitterConsumerKey,
                             ApplicationSettings.TwitterConsumerSecret,
                             twitter_.AccessToken,
                             twitter_.AccessTokenSecret,
                             string.Empty,
                             string.Empty );
        }
    }
}
