﻿'* xPL Library for .NET
'*
'* Version 5.5
'*
'* Copyright (c) 2009-2011 Thijs Schreijer
'* http://www.thijsschreijer.nl
'*
'* Copyright (c) 2008-2009 Tom Van den Panhuyzen
'* http://blog.boxedbits.com/xpl
'*
'* Copyright (C) 2003-2005 John Bent
'* http://www.xpl.myby.co.uk
'*
'* This program is free software; you can redistribute it and/or
'* modify it under the terms of the GNU General Public License
'* as published by the Free Software Foundation; either version 2
'* of the License, or (at your option) any later version.
'* 
'* This program is distributed in the hope that it will be useful,
'* but WITHOUT ANY WARRANTY; without even the implied warranty of
'* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'* GNU General Public License for more details.
'*
'* You should have received a copy of the GNU General Public License
'* along with this program; if not, write to the Free Software
'* Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
'* Linking this library statically or dynamically with other modules is
'* making a combined work based on this library. Thus, the terms and
'* conditions of the GNU General Public License cover the whole
'* combination.
'* As a special exception, the copyright holders of this library give you
'* permission to link this library with independent modules to produce an
'* executable, regardless of the license terms of these independent
'* modules, and to copy and distribute the resulting executable under
'* terms of your choice, provided that you also meet, for each linked
'* independent module, the terms and conditions of the license of that
'* module. An independent module is a module which is not derived from
'* or based on this library. If you modify this library, you may extend
'* this exception to your version of the library, but you are not
'* obligated to do so. If you do not wish to do so, delete this
'* exception statement from your version.

Option Strict On
Imports xPL
Imports xPL.xPL_Base
Imports System.Text

''' <summary>
''' represents a fragmented xPL message, either received from the xPL network, or created from an internal message as preparation to send it.
''' </summary>
''' <remarks></remarks>
Public Class xPLFragmentedMsg
    Implements IDisposable

    ''' <summary>
    ''' Exception thrown incase of fragmentation related errors
    ''' </summary>
    ''' <remarks></remarks>
    Public Class FragmentationException
        Inherits Exception
        Private Sub New()   ' private, not accessible
        End Sub
        Public Sub New(ByVal Message As String, Optional ByVal InnerException As Exception = Nothing)
            MyBase.New(Message, InnerException)
        End Sub
    End Class

    ''' <summary>
    ''' Simple class to dissect a fragment key into its components; fragment nr, total number of fragments and message ID
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class FragmentKey
        ''' <summary>
        ''' From the 'partid' key this is part x in 'partid=x/y:z'
        ''' </summary>
        ''' <remarks></remarks>
        Public FragmentNumber As Integer = 0
        ''' <summary>
        ''' From the 'partid' key this is part y in 'partid=x/y:z'
        ''' </summary>
        ''' <remarks></remarks>
        Public FragmentTotal As Integer = 0
        ''' <summary>
        ''' From the 'partid' key this is part z in 'partid=x/y:z'
        ''' </summary>
        ''' <remarks></remarks>
        Public MessageID As String = ""
        ''' <summary>
        ''' Returns the fragmentkey as a formatted fragment key;  'nr/max:id'
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overrides Function ToString() As String
            Return FragmentNumber.ToString & "/" & FragmentTotal.ToString & ":" & MessageID
        End Function
        Private Sub DissectKey(ByVal FragKey As String)
            Try
                Dim n1 As Integer = FragKey.IndexOf("/")
                Dim n2 As Integer = FragKey.IndexOf(":")
                FragmentNumber = CInt(Left(FragKey, n1))
                If FragmentNumber < 1 Then Throw New IndexOutOfRangeException("Fragment number must be 1 or greater")
                FragmentTotal = CInt(Mid(FragKey, n1 + 2, n2 - n1 - 1))
                If FragmentTotal < 1 Then Throw New IndexOutOfRangeException("Number of fragments must be 1 or greater")
                MessageID = Mid(FragKey, n2 + 2)
                '                Debug.Print("Key dissected; 'ID=" & MessageID & "'")
                If Trim(MessageID) = "" Then Throw New ArgumentException("The provided message ID cannot be an empty (or all space) string")
            Catch ex As Exception
                Throw New FragmentationException("Cannot extract fragmentnr, number of fragments and/or message ID from the 'fragment' key in the message. Key provided : 'fragment=" & FragKey & "'.", ex)
            End Try

        End Sub
        ''' <summary>
        ''' Dissects fragmentkey 'nr/max:id' into its underlying components as a FragmentKey object
        ''' </summary>
        ''' <param name="FragKey"></param>
        ''' <remarks></remarks>
        Public Sub New(ByVal FragKey As String)
            Me.DissectKey(FragKey)
        End Sub
        ''' <summary>
        ''' Dissects the partidkey 'nr/max:id' from the provided message into its underlying components as a FragmentKey object
        ''' </summary>
        ''' <param name="msg"></param>
        ''' <remarks></remarks>
        Public Sub New(ByVal msg As xPLMessage)
            Dim FragKey As String
            Try
                FragKey = msg.KeyValueList("partid")
            Catch ex As Exception
                Throw New FragmentationException("Cannot find 'partid' key in provided message", ex)
            End Try
            Me.DissectKey(FragKey)
        End Sub
    End Class
    ''' <summary>
    ''' Will hold the original xPLMessage object that created, or was reconstructed from, the fragments
    ''' </summary>
    ''' <remarks></remarks>
    Private _Message As xPLMessage
    ''' <summary>
    ''' A checklist containing the numbers of the parts still missing/expected, if the list is empty, the message is complete.
    ''' </summary>
    ''' <remarks></remarks>
    Private _Checklist As New ArrayList
    ''' <summary>
    ''' Holds all the individual message fragments (xPLMessage objects), by their fragment number
    ''' </summary>
    ''' <remarks></remarks>
    Private _Fragments As New SortedList(Of Integer, xPLMessage)
    ''' <summary>
    ''' Returns the individual message fragments (xPLMessage objects), by their fragment number, ranging from 1 to <see cref="Count">Count</see>.
    ''' If the fragment is unavailable <c>Nothing</c> is returned. If the index is out of range, an exception is thrown.
    ''' </summary>
    ''' <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is less then 1 or greater than <see cref="Count">Count</see></exception>
    ''' <remarks></remarks>
    Public ReadOnly Property Fragment(ByVal index As Integer) As xPLMessage
        Get
            If index < 1 Or index > Me.Count Then Throw New IndexOutOfRangeException(index.ToString & " is not supported, values from 1 to Count (" & Me.Count.ToString & ") only")
            If _Fragments.ContainsKey(index) Then
                Return _Fragments(index)
            Else
                Return Nothing
            End If
        End Get
    End Property
    Private _Source As String
    ''' <summary>
    ''' The source address the message originates from
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property Source() As String
        Get
            If Created Then Return Parent.Address
            Return _Source
        End Get
    End Property
    Private _Parent As xPLDevice
    ''' <summary>
    ''' The parent device the message belongs to
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property Parent() As xPLDevice
        Get
            Return _Parent
        End Get
    End Property
    Private _MessageID As String    ' senderaddress & ":" & (ID in partid key)
    ''' <summary>
    ''' The message ID of a fragmented message is the senders xPL address with the message specific ID 
    ''' in the partid key, separated by a colon ':'. For example 'tieske-mydev.instance:124'.
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property MessageID() As String
        Get
            Return _MessageID
        End Get
    End Property
    Private _Count As Integer
    ''' <summary>
    ''' Total number of fragments for the message
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property Count() As Integer
        Get
            Return _Count
        End Get
    End Property

    ''' <summary>
    ''' If True, the message was received, false it was created
    ''' </summary>
    ''' <remarks></remarks>
    Private _blnReceived As Boolean
    ''' <summary>
    ''' Returns <c>True</c> if the message was received from the network, this allows other fragments to be added to this message.
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property Received() As Boolean
        Get
            Return _blnReceived
        End Get
    End Property
    ''' <summary>
    ''' Returns <c>True</c> if the message was self created (and not received), this prevents other fragments of being added to this message
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property Created() As Boolean
        Get
            Return Not _blnReceived
        End Get
    End Property

    ''' <summary>
    ''' Creates a new fragmented message, if the provided message has a 'fragment.basic' schema then it becomes a
    ''' 'received' message, otherwise it becomes a 'created' message.
    ''' </summary>
    ''' <param name="msg"></param>
    ''' <remarks></remarks>
    Public Sub New(ByVal msg As xPLMessage, ByVal Parent As xPLDevice)
        If msg Is Nothing Then Throw New ArgumentException("Must provide an xPLMessage object.", "msg")
        If Parent Is Nothing Then Throw New ArgumentException("Must provide an xPLDevice object as Parent.", "Parent")
        _Parent = Parent
        If msg.Schema = "fragment.basic" Then
            CreateFromReceived(msg)
        Else
            CreateFromMessage(msg)
        End If
        Parent._FragmentedMessageList.Add(Me, Me.MessageID)
    End Sub

    Private Sub CreateFromReceived(ByVal msg As xPLMessage)
        _blnReceived = True
        _Fragments.Clear()
        _Checklist.Clear()
        Dim FragKey As New FragmentKey(msg)
        Me._MessageID = msg.Source & ":" & FragKey.MessageID
        Me._Count = FragKey.FragmentTotal
        Me._Message = New xPLMessage
        Me._Source = msg.Source
        With Me._Message
            .MsgType = msg.MsgType
            .Hop = msg.Hop
            .Source = msg.Source
            .Target = msg.Target
        End With
        ' Fill checklist with all message fragments, 1 to max, to be checked off when receiving them
        For n As Integer = 1 To FragKey.FragmentTotal
            _Checklist.Add(n)
        Next
        Me.AddFragment(msg)
    End Sub
    Private Sub CreateFromMessage(ByVal msg As xPLMessage)
        _blnReceived = False

        Dim frag As xPLMessage = Nothing
        Dim done1 As Boolean = False
        Dim done2 As Boolean = False
        Dim cnt As Integer = 0
        Dim index As Integer = 0
        Dim bytesleft As Integer = 0
        While Not done1
            cnt = cnt + 1
            If cnt > XPL_FRAGMENT_MAX Then
                Throw New FragmentationException("Message too large, results in more than the maximum number of allowed fragments (" & XPL_FRAGMENT_MAX.ToString & ").")
            End If
            frag = New xPLMessage
            frag.MsgType = msg.MsgType
            frag.Source = msg.Source
            frag.Target = msg.Target
            frag.Hop = msg.Hop
            frag.Schema = "fragment.basic"
            frag.KeyValueList.Add("partid", XPL_FRAGMENT_MAX & "/" & XPL_FRAGMENT_MAX & ":" & XPL_FRAGMENT_COUNTER_MAX)
            If cnt = 1 Then
                frag.KeyValueList.Add("schema", msg.Schema)
            End If
            bytesleft = XPL_MAX_MSG_SIZE - frag.RawxPL.Length
            done2 = False
            While Not done2 And msg.KeyValueList.Count > 0
                ' get size of next key/value pair
                Dim b As Integer = Encoding.UTF8.GetByteCount(msg.KeyValueList(index).ToString) + 1
                If b <= bytesleft Then
                    ' still fits in this fragment, so add it
                    frag.KeyValueList.Add(msg.KeyValueList(index).Key, msg.KeyValueList(index).Value)
                    index = index + 1
                    bytesleft = bytesleft - b
                    done2 = (index >= msg.KeyValueList.Count)
                Else
                    ' won't fit anymore
                    If (cnt = 1 And frag.KeyValueList.Count = 2) Or (cnt > 1 And frag.KeyValueList.Count = 1) Then
                        ' nothing was added, so key/value at position 'index' is too large to fit
                        Throw New FragmentationException("Cannot fragment; key/value pair at position " & index & " is too large for a single message.")
                    End If
                    ' move to next fragment
                    done2 = True
                End If
            End While
            ' fragment construction done
            _Fragments.Add(cnt, frag)
            done1 = (index = msg.KeyValueList.Count)
        End While
        ' set all the proper IDs
        Dim msgid As Integer = Parent.GetNewFragmentedID
        For n As Integer = 1 To _Fragments.Count
            _Fragments(n).KeyValueList.Item("partid") = n.ToString & "/" & cnt.ToString & ":" & msgid.ToString
        Next
        ' set other properties
        _Message = msg
        _MessageID = Parent.Address & ":" & msgid.ToString
        _Count = cnt
    End Sub

    ''' <summary>
    ''' If the object is set as received from the network, this methods allows for the addition of new fragments received. Only if the
    ''' partid matches the existing parts it will be added. No exceptions will be thrown if it doesn't match.
    ''' Returns either te reconstructed message, if its the last fragment, or <c>Nothing</c>.
    ''' </summary>
    ''' <param name="msg"></param>
    ''' <remarks></remarks>
    Public Function AddFragment(ByVal msg As xPLMessage) As xPLMessage
        If _disposed Then Return Nothing
        Dim FragKey As New FragmentKey(msg)
        'Debug.Print("xPLFragmentedMsg.AddFragment: Received a fragment from " & msg.Source & " with id; " & FragKey.ToString)
        If Parent.Debug Then LogError("xPLFragmentedMsg.AddFragment", "Received a fragment from " & msg.Source & " with id; " & FragKey.ToString)
        If Me.Created Then Throw New InvalidOperationException("Cannot add fragments to a created message, only to received messages.")
        If msg.Source & ":" & FragKey.MessageID <> Me.MessageID Then Return Nothing
        If FragKey.FragmentNumber = 1 Then
            If (msg.KeyValueList.IndexOf("schema") <> -1) Then
                ' got the schema, go set it
                Try
                    _Message.Schema = msg.KeyValueList("schema")
                Catch ex As Exception
                    Parent.LogMessage("Received fragment from " & msg.Source & " with an invalid schema in the 1st message; 'schema=" & msg.KeyValueList("schema") & "'.", xPLDevice.xPLLogLevels.Warning)
                    Throw New FragmentationException("Fragmented message contained an illegal schema value; 'schema=" & msg.KeyValueList("schema") & "'.")
                End Try
            Else
                Parent.LogMessage("Received fragment from " & msg.Source & " without a 'schema' key in the 1st message.", xPLDevice.xPLLogLevels.Warning)
                Throw New FragmentationException("1st fragment does not contain the 'schema' key")
            End If
        End If

        If Not _Checklist.Contains(FragKey.FragmentNumber) Then
            ' fragment number wasn't in the checklist, so we already had this one. Do nothing.
        Else
            ' the fragment number is still in the checklist, so we were waiting for this one, go process it
            _Checklist.Remove(FragKey.FragmentNumber)   ' remove it so we won't process it again
            _Fragments.Add(FragKey.FragmentNumber, msg) ' add to our list of fragments
            If _Checklist.Count = 0 Then
                ' we've got all fragments, so go reconstruct the message
                _ResendTimer.Stop()
                Me.Reconstruct()
                If IsComplete Then
                    ' Inform parent of received message, and dismiss myself
                    Dim m As xPLMessage = _Message  ' create local copy, can be called while 'me' is disposed
                    Dim p As xPLDevice = _Parent    ' create local copy, can be called while 'me' is disposed
                    Me.Dispose()                    ' releases/stops timers
                    p.IncomingMessage(m)   ' reference the local copies, necessary? just to be sure!
                    Return m
                End If
            End If
        End If
        If Not IsComplete Then ResetResendTimer()
        Return Nothing
    End Function

    ''' <summary>
    ''' Once the last fragment has been received, this will reconstruct the original message. Header is already done, now restore key-valuepairs in correct order
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub Reconstruct()
        Dim msgfrag As xPLMessage

        ' clear current list, will be rebuild.
        _Message.KeyValueList.Clear()

        ' loop through all fragments
        For i As Integer = 1 To _Fragments.Count
            msgfrag = _Fragments(i)

            Dim SkipPartID As Boolean = True     ' first key named 'partid' must be skipped, is fragment.basic overhead
            Dim SkipSchema As Boolean = (i = 1)    ' only if its the 1st fragment, also the first key 'schema' must be handled separately
            For n As Integer = 1 To msgfrag.KeyValueList.Count
                Dim kv As xPLKeyValuePair = msgfrag.KeyValueList(n - 1)
                If kv.Key = "partid" And SkipPartID Then
                    ' must skip this partid key
                    SkipPartID = False  ' only the first, so now disable
                Else
                    If kv.Key = "schema" And SkipSchema Then
                        ' must skip this schema key
                        SkipSchema = False    ' only the first, so now disable
                    Else
                        ' add this key to the reconstructed message
                        _Message.KeyValueList.Add(kv)
                    End If
                End If
            Next
        Next
        ' message completed, now add to ignore list
        Me._Parent._FragmentIgnoreList.Add(Now().AddMinutes(5), _MessageID)
        ' remove any expired items from the list
        For n As Integer = Me._Parent._FragmentIgnoreList.Count To 1 Step -1
            If CDate(Me._Parent._FragmentIgnoreList(n)) < Now() Then
                ' item expired, so remove it
                Me._Parent._FragmentIgnoreList.Remove(n)
            End If
        Next
    End Sub

    ''' <summary>
    ''' Returns <c>True</c> if the message is complete. Only usefull for received messages, which wait for other fragments to come in.
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property IsComplete() As Boolean
        Get
            Return (_Checklist.Count = 0)
        End Get
    End Property

    ''' <summary>
    ''' Requests the still missing parts of the message through the provided device (will be used as the sender for the request). If no 
    ''' device provided an active device will be gotten from the xPLListener to send it through.
    ''' </summary>
    ''' <param name="dev"></param>
    ''' <remarks></remarks>
    Private Sub RequestMissingParts(ByVal dev As xPLDevice)
        If Me.Created Then Throw New FragmentationException("Can only request missing parts for received fragmented messages, not for created ones")
        If dev Is Nothing Then Throw New NullReferenceException("No device provided")
        Dim msg As New xPLMessage
        msg.Target = Me.Source
        msg.MsgType = xPLMessageTypeEnum.Command
        msg.Schema = "fragment.request"
        msg.KeyValueList.Add("command", "resend")
        msg.KeyValueList.Add("message", Mid(MessageID, MessageID.IndexOf(":") + 2))
        For Each nr As Integer In _Checklist
            msg.KeyValueList.Add("part", nr.ToString)
        Next
        If dev.Status <> xPLDeviceStatus.Offline Then
            dev.Send(msg)
        End If
    End Sub

    ''' <summary>
    ''' Will resend requested parts, as requested in the provided message. If the partid doesn't match, nothing will be send and 
    ''' no exception will be thrown. <c>True</c> is returned when the parts have been succesfully resend.
    ''' </summary>
    ''' <param name="msg">Message containing the resend request</param>
    ''' <remarks></remarks>
    Public Sub ResendFailedParts(ByVal msg As xPLMessage)
        If msg Is Nothing Then Throw New NullReferenceException("No message provided")
        If msg.Schema <> "fragment.request" Then Throw New ArgumentException("Message schema type is not 'fragment.request'.", "msg")
        If msg.KeyValueList.IndexOf("message") = -1 Then
            Parent.LogMessage("Received 'fragment.request' message from " & msg.Source & " without a 'message' key containing the message id. Message will be ignored.", xPLDevice.xPLLogLevels.Warning)
        End If
        Dim mid As String = Parent.Address & ":" & msg.KeyValueList("message")
        If mid = MessageID Then
            If msg.KeyValueList("command") <> "resend" Then
                Parent.LogMessage("Received 'fragment.request' message from " & msg.Source & " with an unknown command; 'command=" & msg.KeyValueList("command") & "' . Message will be ignored.", xPLDevice.xPLLogLevels.Warning)
            End If
            If msg.KeyValueList.IndexOf("part") = -1 Then
                Parent.LogMessage("Received 'fragment.request' message from " & msg.Source & " without a 'part' key containing the fragment number requested. Message will be ignored.", xPLDevice.xPLLogLevels.Warning)
            End If
            For n As Integer = 0 To msg.KeyValueList.Count - 1
                If msg.KeyValueList(n).Key = "part" Then
                    Dim part As Integer = Integer.Parse(msg.KeyValueList(n).Value)
                    If _Fragments.ContainsKey(part) Then
                        If Parent.Status <> xPLDeviceStatus.Offline Then Parent.Send(_Fragments(part))
                    Else
                        Parent.LogMessage("Received 'fragment.request' message from " & msg.Source & " for 'part=" & part & "'. Part (fragment) not found. Message will be ignored.", xPLDevice.xPLLogLevels.Warning)
                    End If
                End If
            Next
            ResetDismissTimer()
        Else
            ' its not me, so do nothing
        End If
    End Sub

    ''' <summary>
    ''' Send the fragmented message through its parent device. Will send all fragments.
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Send()
        If Received Then Throw New FragmentationException("Cannot send a message that was received, only created ones.")

        For n As Integer = 1 To Count
            'Debug.Print("Sending fragment " & n & "/" & Count)
            If Parent.Debug Then LogError("xPLFragmentedMsg.Send", "Sending fragment " & n.ToString & "/" & Count.ToString, EventLogEntryType.Information)
            Parent.Send(_Fragments(n))
        Next
        If Parent.Debug Then LogError("xPLFragmentedMsg.Send", "Sending fragmented message complete.", EventLogEntryType.Information)
        ResetDismissTimer()
    End Sub

    ''' <summary>
    ''' Timer for dismissing a send message. Will expire when the message fragments can no longer be requested by other devices
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents _DismissTimer As New Timers.Timer
    ''' <summary>
    ''' Timer that will be set after receiving a fragment, when it expires a resend request needs to be fired for the missing 
    ''' fragments, and the timer will be reused for expiring the incomplete message (if no new fragments are received after
    ''' the resend request).
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents _ResendTimer As New Timers.Timer
    ''' <summary>
    ''' If this is <c>False</c> when the <see cref="_ResendTimer">_ResendTimer</see> expires, then it expired for the second 
    ''' time, which means the incoming message should be dismissed.
    ''' </summary>
    ''' <remarks></remarks>
    Private _ReceivedFragmentSinceLastResendRequest As Boolean = True
    ''' <summary>
    ''' Timer that will expire when the message can be dismissed, when no new resend requests will be handled
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ResetDismissTimer()
        _DismissTimer.Stop()
        _DismissTimer.AutoReset = False
        _DismissTimer.Interval = XPL_FRAGMENT_SEND_RETAIN
        _DismissTimer.Start()
    End Sub
    Private Sub DismissTimerExpired() Handles _DismissTimer.Elapsed
        If _disposed Then Exit Sub
        ' Retainment period has expired, cleanup and remove this message
        Me.Dispose()
    End Sub
    ''' <summary>
    ''' Timer that will be set when a fragment is received, if not timely all fragments are in, this timer requests a resend
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ResetResendTimer()
        _ReceivedFragmentSinceLastResendRequest = True
        _ResendTimer.Stop()
        _ResendTimer.AutoReset = False
        _ResendTimer.Interval = XPL_FRAGMENT_REQUEST_AFTER
        _ResendTimer.Start()
    End Sub
    Private Sub ResendTimerElapsed() Handles _ResendTimer.Elapsed
        If _disposed Then Exit Sub
        If _ReceivedFragmentSinceLastResendRequest Then
            ' first time timer expires, so request a resend
            _ReceivedFragmentSinceLastResendRequest = False
            _ResendTimer.Stop()
            _ResendTimer.Interval = XPL_FRAGMENT_REQUEST_TIMEOUT
            _ResendTimer.Start()
            Me.RequestMissingParts(Parent)
        Else
            ' second time it expires without fragments having been received, dismiss the message
            LogError("xPLFragmentedMsg.ResendTimerElapsed", "Fragment resend request for message " & Me.MessageID & " did not result in all fragments, message dismissed as incomplete.", EventLogEntryType.Warning)
            Parent.LogMessage("Fragment resend request for message " & Me.MessageID & " did not result in all fragments, message dismissed as incomplete.", xPLDevice.xPLLogLevels.Warning)
            Me.Dispose()
        End If
    End Sub

    Private _disposed As Boolean = False        ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not Me._disposed Then
            'Debug.Print("Disposing of fragmented message for: " & Parent.Address)
            If disposing Then
                ' free other state (managed objects).

            End If

            ' free your own state (unmanaged objects).
            ' set large fields to null.
            Try
                _ResendTimer.Stop()
            Catch ex As Exception
            End Try

            Try
                _DismissTimer.Stop()
            Catch ex As Exception
            End Try

            Try
                If Parent._FragmentedMessageList.Contains(Me.MessageID) Then
                    Parent._FragmentedMessageList.Remove(Me.MessageID)
                End If
            Catch ex As Exception
            End Try
        End If
        Me._disposed = True
    End Sub

#Region " IDisposable Support "
    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
