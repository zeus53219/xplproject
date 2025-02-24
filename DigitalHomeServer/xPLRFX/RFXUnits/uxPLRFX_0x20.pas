(***********************************************************)
(* xPLRFX                                                  *)
(* part of Digital Home Server project                     *)
(* http://www.digitalhomeserver.net                        *)
(* info@digitalhomeserver.net                              *)
(***********************************************************)
unit uxPLRFX_0x20;

interface

Uses uxPLRFXConst, u_xPL_Message, u_xpl_common, uxPLRFXMessages;

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);

implementation

Uses SysUtils;

(*

Type $20 - Security1 - X10, KD101, Visionic, Melantech

Buffer[0] = packetlength = $08;
Buffer[1] = packettype
Buffer[2] = subtype
Buffer[3] = seqnbr
Buffer[4] = id1
Buffer[5] = id2
Buffer[6] = id3
Buffer[7] = status
Buffer[8] = battery_level:4/rssi:4

Test Strings :

0820004DD3DC540089


xPL Schema

x10.security
{
  command=alert|normal|motion|light|dark|arm-home|arm-away|disarm|panic|lights-on|lights-off
  device=<device id>
  protocol=x10-door|x10-motion|x10-remote|kd101|visionic-door|visionic-motion|melantech
  [tamper=true|false]
  [low-battery=true|false]
  [delay=min|max]
}


*)

const
  // Packet length
  PACKETLENGTH     = $08;

  // Type
  SECURITY1        = $20;

  // Subtype
  X10_DOOR          = $00;
  X10_MOTION        = $01;
  X10_REMOTE        = $02;
  KD101             = $03;
  VISIONIC_DOOR     = $04;
  VISIONIC_MOTION   = $05;
  MELANTECH         = $08;
  // SubTypeStrings
  X10_DOOR_STR         = 'x10-door';
  X10_MOTION_STR       = 'x10-motion';
  X10_REMOTE_STR       = 'x10-remote';
  KD101_STR            = 'kd101';
  VISIONIC_DOOR_STR    = 'visionic-door';
  VISIONIC_MOTION_STR  = 'visionic-motion';
  MELANTECH_STR        = 'melantech';

  // Commands
  COMMAND_ALERT      = 'alert';
  COMMAND_NORMAL     = 'normal';
  COMMAND_MOTION     = 'motion';
  COMMAND_LIGHT      = 'light';
  COMMAND_DARK       = 'dark';
  COMMAND_ARM_HOME   = 'arm-home';
  COMMAND_ARM_AWAY   = 'arm-away';
  COMMAND_DISARM     = 'disarm';
  COMMAND_PANIC      = 'panic';
  COMMAND_LIGHTS_ON  = 'lights-on';
  COMMAND_LIGHTS_OFF = 'lights-off';

var
  SubTypeArray : array[1..7] of TRFXSubTypeRec =
    ((SubType : X10_DOOR; SubTypeString : X10_DOOR_STR),
     (SubType : X10_MOTION; SubTypeString : X10_MOTION_STR),
     (SubType : X10_REMOTE; SubTypeString : X10_REMOTE_STR),
     (SubType : KD101; SubTypeString : KD101_STR),
     (SubType : VISIONIC_DOOR; SubTypeString : VISIONIC_DOOR_STR),
     (SubType : VISIONIC_MOTION; SubTypeString : VISIONIC_MOTION_STR),
     (SubType : MELANTECH; SubTypeString : MELANTECH_STR));

//?? RFXCOMMANDARRAY for security ??
// TO CHECK :  how this works


procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
var
  DeviceID : String;
  Command : String;
  SubType : Byte;
  Tamper : Boolean;
  BatteryLevel : Integer;
  Delay : Boolean;
  xPLMessage : TxPLMessage;
begin
  SubType := Buffer[2];
  DeviceID := '0x'+IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2)+IntToHex(Buffer[6],2);
  case Buffer[7] of
    $00, $01, $05, $7, $80, $81, $85 : Command := COMMAND_NORMAL;
    $02, $03, $82,$83 : Command := COMMAND_ALERT;
    $04, $84 : Command := COMMAND_MOTION;
    $06 : Command := COMMAND_PANIC;
    $09, $0A : Command := COMMAND_ARM_AWAY;
    $0B, $0C : Command := COMMAND_ARM_HOME;
    $0D : Command := COMMAND_DISARM;
    $10, $12 : Command := COMMAND_LIGHTS_OFF;
    $11, $13 : Command := COMMAND_LIGHTS_ON;
  end;
  case Buffer[7] of
    $01, $03, $0A, $0C, $81, $83 : Delay := True;
  end;
  case Buffer[7] of
    $80, $82, $84, $85 : Tamper := True;
  end;
  if (Buffer[8] and $0F) = 0 then  // zero out rssi
    BatteryLevel := 0
  else
    BatteryLevel := 100;

  // Create control.basic message
  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'x10.security';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+DeviceID);
  xPLMessage.Body.AddKeyValue('protocol='+GetSubTypeString(Buffer[2],SubTypeArray));
  xPLMessage.Body.AddKeyValue('command='+Command);
  if Tamper then
    xPLMessage.Body.AddKeyValue('tamper=true')
  else
    xPLMessage.Body.AddKeyValue('tamper=false');
  if Delay then
    xPLMessage.Body.AddKeyValue('delay=max')
  else
    xPLMessage.Body.AddKeyValue('delay=min');
  if BatteryLevel = 0 then
    xPLMessage.Body.AddKeyValue('low-battery=true');
  xPLMessages.Add(xPLMessage.RawXPL);
end;

procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);
var
  Command : String;
  Tamper : String;
  Delay : String;
begin
  ResetBuffer(Buffer);
  Buffer[0] := PACKETLENGTH;
  Buffer[1] := SECURITY1;  // Type
  Buffer[2] := GetSubTypeFromString(aMessage.Body.Strings.Values['protocol'],SubTypeArray);  // Subtype
  // DeviceID
  Buffer[4] := StrToInt('$'+Copy(aMessage.Body.Strings.Values['device'],3,2));
  Buffer[5] := StrToInt('$'+Copy(aMessage.Body.Strings.Values['device'],5,2));
  Buffer[6] := StrToInt('$'+Copy(aMessage.Body.Strings.Values['device'],7,2));
  // Command
  Command := aMessage.Body.Strings.Values['command'];
  Tamper := aMessage.Body.Strings.Values['tamper'];
  Delay := aMessage.Body.Strings.Values['delay'];
  if CompareText(Command,COMMAND_NORMAL) = 0 then
    begin
      if (CompareText(Tamper,'true') = 0) and (CompareText(Delay,'true') = 0) then
        Buffer[7] := $81 else
      if CompareText(Tamper,'true') = 0 then
        Buffer[7] := $80 else
      if CompareText(Delay,'true') = 0 then
        Buffer[7] := $01 else
      Buffer[7] := $00;
    end else
  if CompareText(Command,COMMAND_ALERT) = 0 then
    begin
      if (CompareText(Tamper,'true') = 0) and (CompareText(Delay,'true') = 0) then
        Buffer[7] := $83 else
      if CompareText(Tamper,'true') = 0 then
        Buffer[7] := $82 else
      if CompareText(Delay,'true') = 0 then
        Buffer[7] := $03 else
      Buffer[7] := $02;
    end else
  if CompareText(Command,COMMAND_MOTION) = 0 then
    begin
      if CompareText(Tamper,'true') = 0 then
        Buffer[7] := $84
      else
        Buffer[7] := $04;
    end else
  if CompareText(Command,COMMAND_PANIC) = 0 then
    begin
      Buffer[7] := $06;
    end else
  if CompareText(Command,COMMAND_ARM_AWAY) = 0 then
    begin
      if CompareText(Delay,'true') = 0 then
        Buffer[7] := $0A
      else
        Buffer[7] := $09;
    end else
  if CompareText(Command,COMMAND_ARM_HOME) = 0 then
    begin
      if CompareText(Delay,'true') = 0 then
        Buffer[7] := $0C
      else
        Buffer[7] := $0B;
    end else
  if CompareText(Command,COMMAND_LIGHTS_ON) = 0 then
    begin
      Buffer[7] := $11;
    end else
  if CompareText(Command,COMMAND_LIGHTS_OFF) = 0 then
    begin
      Buffer[7] := $10;
    end;
end;

end.
