﻿' Tween - Client of Twitter
' Copyright (c) 2007-2010 kiri_feather (@kiri_feather) <kiri_feather@gmail.com>
'           (c) 2008-2010 Moz (@syo68k) <http://iddy.jp/profile/moz/>
'           (c) 2008-2010 takeshik (@takeshik) <http://www.takeshik.org/>
' All rights reserved.
' 
' This file is part of Tween.
' 
' This program is free software; you can redistribute it and/or modify it
' under the terms of the GNU General Public License as published by the Free
' Software Foundation; either version 3 of the License, or (at your option)
' any later version.
' 
' This program is distributed in the hope that it will be useful, but
' WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
' or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
' for more details. 
' 
' You should have received a copy of the GNU General Public License along
' with this program. If not, see <http://www.gnu.org/licenses/>, or write to
' the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
' Boston, MA 02110-1301, USA.

Imports System.Web
Imports System.Net
Imports System.Collections.Generic

Public Module Outputz
    Private myOuturl As String
    Private myOuturlEncoded As String
    Private myApikey As String
    Private myApikeyEncoded As String

    Private state As Boolean


    'Public Sub New(ByVal _key As String, ByVal _url As String, ByVal _state As Boolean)
    '    url = _url
    '    key = _key
    '    Enabled = _state
    'End Sub

    Public Property url() As String
        Get
            Return myOuturl
        End Get
        Set(ByVal value As String)
            myOuturl = value
            myOuturlEncoded = HttpUtility.UrlEncode(value)
        End Set
    End Property

    Public Property key() As String
        Get
            Return myApikey
        End Get
        Set(ByVal value As String)
            myApikey = value
            myApikeyEncoded = HttpUtility.UrlEncode(value)
        End Set
    End Property

    Public Property Enabled() As Boolean
        Get
            Return state
        End Get
        Set(ByVal value As Boolean)
            state = value
        End Set
    End Property

    Public Function Post(ByVal length As Integer) As Boolean

        If state = False Then Return True

        Dim content As String = ""
        Dim output As String = "http://outputz.com/api/post"
        Dim param As New SortedList(Of String, String)
        param.Add("key", myApikeyEncoded)
        param.Add("uri", myOuturlEncoded)
        param.Add("size", length.ToString)

        Return (New HttpVarious).PostData(output, param)
    End Function
End Module
