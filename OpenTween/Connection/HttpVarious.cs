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
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;


namespace OpenTween
{


    public class HttpVarious : HttpConnection
    {
        public string GetRedirectTo(string url)
        {
            try {
                HttpWebRequest request = CreateRequest( HeadMethod, new Uri( url ), null, false );
                request.Timeout = 5000;
                request.AllowAutoRedirect = false;
                string data;
                IDictionary<string, string> head = new Dictionary<string, string>();
                /* HttpStatusCode status_code = */
                GetResponse( request, out data, head, false );
                
                if ( head.ContainsKey( "Location" ) ) {
                    return head["Location"];
                } else {
                    return url;
                }
            } catch ( Exception ) {
                return url;
            }
        }


        public Image GetImage(Uri url)
        {
            return GetImage( url.ToString() );
        }


        public Image GetImage(string url)
        {
            return GetImage( url, 10000 );
        }


        public Image GetImage(string url, int timeout)
        {
            string error_message;
            
            return GetImage( url, string.Empty, timeout, out error_message );
        }


        public Image GetImage(string url, string referer)
        {
            string error_message;

            return GetImage( url, referer, 10000, out error_message );
        }


        public Image GetImage(string url, string referer, int timeout, out string error_message)
        {
            return GetImageInternal( CheckValidImage, url, referer, timeout, out error_message );
        }


        public Image GetIconImage(string url, int timeout)
        {
            string error_message;
            
            return GetImageInternal( CheckValidIconImage, url, string.Empty, timeout, out error_message );
        }

        private delegate Image CheckValidImageDelegate(Image img,int width,int height);


        private Image GetImageInternal(CheckValidImageDelegate CheckImage, string url, string referer, int timeout, out string error_message)
        {
            try {
                HttpWebRequest request = CreateRequest( GetMethod, new Uri( url ), null, false );
                if ( !String.IsNullOrEmpty( referer ) )
                    request.Referer = referer;
                if ( timeout < 3000 || timeout > 30000 ) {
                    request.Timeout = 10000;
                } else {
                    request.Timeout = timeout;
                }
                Bitmap bitmap;
                HttpStatusCode status_code = GetResponse( request, out bitmap, null, false );
                if ( status_code == HttpStatusCode.OK ) {
                    error_message = string.Empty;
                } else {
                    error_message = status_code.ToString();
                }
                if ( bitmap != null )
                    bitmap.Tag = url;
                if ( status_code == HttpStatusCode.OK )
                    return CheckImage( bitmap, bitmap.Width, bitmap.Height );
                return null;
            } catch ( WebException ex ) {
                error_message = ex.Message;
                return null;
            } catch ( Exception ) {
                error_message = string.Empty;
                return null;
            }
        }


        public bool PostData(string Url, IDictionary<string, string> param)
        {
            try {
                HttpWebRequest request = CreateRequest( PostMethod, new Uri( Url ), param, false );
                HttpStatusCode status_code = this.GetResponse( request, null, false );
                
                if ( status_code == HttpStatusCode.OK )
                    return true;
                
                return false;
            } catch ( Exception ) {
                return false;
            }
        }


        public bool PostData(string Url, IDictionary<string, string> param, out string content)
        {
            try {
                HttpWebRequest request = CreateRequest( PostMethod, new Uri( Url ), param, false );
                HttpStatusCode status_code = this.GetResponse( request, out content, null, false );
                
                if ( status_code == HttpStatusCode.OK )
                    return true;
                
                return false;
            } catch ( Exception ) {
                content = null;
                return false;
            }
        }


        public bool GetData(string Url, IDictionary<string, string> param, out string content, string userAgent)
        {
            string error_message;
            
            return GetData( Url, param, out content, 100000, out error_message, userAgent );
        }


        public bool GetData(string Url, IDictionary<string, string> param, out string content)
        {
            return GetData( Url, param, out content, 100000 );
        }


        public bool GetData(string Url, IDictionary<string, string> param, out string content, int timeout)
        {
            string error_message;
            
            return GetData( Url, param, out content, timeout, out error_message, string.Empty );
        }


        public bool GetData(string Url, IDictionary<string, string> param, out string content, int timeout, out string error_message, string userAgent)
        {
            try {
                HttpWebRequest request = CreateRequest( GetMethod, new Uri( Url ), param, false );
                
                if ( timeout < 3000 || timeout > 100000 ) {
                    request.Timeout = 10000;
                } else {
                    request.Timeout = timeout;
                }
                
                if ( !String.IsNullOrEmpty( userAgent ) )
                    request.UserAgent = userAgent;
                
                HttpStatusCode status_code = this.GetResponse( request, out content, null, false );
                if ( status_code == HttpStatusCode.OK ) {
                    error_message = string.Empty;
                    
                    return true;
                }
                error_message = status_code.ToString();
                
                return false;
            } catch ( Exception ex ) {
                content = null;
                error_message = ex.Message;
                
                return false;
            }
        }


        public HttpStatusCode GetContent(string method, Uri Url, IDictionary<string, string> param, out string content, IDictionary<string, string> headerInfo, string userAgent)
        {
            //Searchで使用。呼び出し元で例外キャッチしている。
            HttpWebRequest request = CreateRequest( method, Url, param, false );
            request.UserAgent = userAgent;
            
            return this.GetResponse( request, out content, headerInfo, false );
        }


        public bool GetDataToFile(string Url, string savePath)
        {
            try {
                HttpWebRequest request = CreateRequest( GetMethod, new Uri( Url ), null, false );
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                request.UserAgent = MyCommon.GetUserAgentString();
                
                using ( FileStream file_stream = new FileStream( savePath, FileMode.Create, FileAccess.Write ) ) {
                    try {
                        HttpStatusCode status_code = this.GetResponse( request, file_stream, null, false );
                        
                        if ( status_code == HttpStatusCode.OK )
                            return true;
                        
                        return false;
                    } catch ( Exception ) {
                        return false;
                    }
                }
            } catch ( Exception ) {
                return false;
            }
        }


        private Image CheckValidIconImage(Image img, int width, int height)
        {
            return CheckValidImage( img, 48, 48 );
        }


        public Image CheckValidImage(Image img, int width, int height)
        {
            if ( img == null )
                return null;

            Bitmap bitmap = null;

            try {
                bitmap = new Bitmap( width, height );
                using (Graphics g = Graphics.FromImage(bitmap)) {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage( img, 0, 0, width, height );
                }
                bitmap.Tag = img.Tag;

                Bitmap result = bitmap;
                bitmap = null; //返り値のBitmapはDisposeしない
                
                return result;
            } catch ( Exception ) {
                if ( bitmap != null ) {
                    bitmap.Dispose();
                    bitmap = null;
                }

                bitmap = new Bitmap( width, height );
                bitmap.Tag = img.Tag;

                Bitmap result = bitmap;
                bitmap = null; //返り値のBitmapはDisposeしない
                
                return result;
            } finally {
                if ( bitmap != null )
                    bitmap.Dispose();
                img.Dispose();
            }
        }
    }
}