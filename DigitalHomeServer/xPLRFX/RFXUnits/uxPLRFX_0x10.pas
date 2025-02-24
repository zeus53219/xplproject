(***********************************************************)
(* xPLRFX                                                  *)
(* part of Digital Home Server project                     *)
(* http://www.digitalhomeserver.net                        *)
(* info@digitalhomeserver.net                              *)
(***********************************************************)
unit uxPLRFX_0x10;

interface

Uses SysUtils, uxPLRFXConst, u_xpl_message, u_xpl_common, uxPLRFXMessages;

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);

implementation

(*

Type $10 - Lighting1 - X10, ARC, ELRO, Waveman, EMW200, Impuls, RisingSun, Philips, Energenie

Buffer[0] = packetlength = $07
Buffer[1] = packettype
Buffer[2] = subtype
Buffer[3] = sequence number
Buffer[4] = housecode
Buffer[5] = unitcode
Buffer[6] = cmnd
Buffer[7] = filler:4/rssi:4

Test Strings :

071000B7490A0160
071000E0490C0060
0710010E430E0180

xPL Schema

x10.basic
{
  device=<housecode[unitcode]
  command=on|off|dim|bright|all_lights_on|all_lights_off|chime
  [protocol=arc]
}

*)

const
  // Packet length
  PACKETLENGTH  = $07;

  // Type
  LIGHTING1     = $10;

  // Subtype
  X10           = $00;
  ARC           = $01;
  ELBRO         = $02;
  WAVEMAN       = $03;
  CHACON_EMW200 = $04;
  IMPULS        = $05;
  RISINGSUN     = $06;
  PHILIPS_SBC   = $07;
  ENERGENIE     = $08;

  // Commands
  COMMAND_OFF      = 'off';
  COMMAND_ON       = 'on';
  COMMAND_DIM      = 'dim';
  COMMAND_BRIGHT   = 'bright';
  COMMAND_GROUPOFF = 'group off';
  COMMAND_GROUPON  = 'group on';
  COMMAND_CHIME    = 'chime';

var
  // Lookup table for commands
  RFXCommandArray : array[1..7] of TRFXCommandRec =
    ((RFXCode : $00; xPLCommand : COMMAND_OFF),
     (RFXCode : $01; xPLCommand : COMMAND_ON),
     (RFXCode : $02; xPLCommand : COMMAND_DIM),
     (RFXCode : $03; xPLCommand : COMMAND_BRIGHT),
     (RFXCode : $05; xPLCommand : COMMAND_GROUPOFF),
     (RFXCode : $06; xPLCommand : COMMAND_GROUPON),
     (RFXCode : $07; xPLCommand : COMMAND_CHIME));

  SubTypeArray : array[1..9] of TRFXSubTypeRec =
    ((SubType : X10; SubTypeString : 'x10'),
     (SubType : ARC; SubTypeString : 'arc'),
     (SubType : ELBRO; SubTypeString : 'elbro'),
     (SubType : WAVEMAN; SubTypeString : 'waveman'),
     (SubType : CHACON_EMW200; SubTypeString : 'chacon'),
     (SubType : IMPULS; SubTypeString : 'impuls'),
     (SubType : RISINGSUN; SubTypeString : 'risingsun'),
     (SubType : PHILIPS_SBC; SubTypeString : 'philips'),
     (SubType : ENERGENIE; SubTypeString : 'energenie'));

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
var
  HouseCode : Byte;
  UnitCode : Byte;
  Command : String;
  xPLMessage : TxPLMessage;
begin
  HouseCode := Buffer[4];
  UnitCode := Buffer[5];
  Command := GetxPLCommand(Buffer[6],RFXCommandArray);

  // Create x10.basic message
  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'x10.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+Chr(HouseCode)+IntToStr(UnitCode));
  xPLMessage.Body.AddKeyValue('command='+Command);
  xPLMessage.Body.AddKeyValue('protocol='+GetSubTypeString(Buffer[2],SubTypeArray));
  xPLMessages.Add(xPLMessage.RawXPL);
end;

procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);
// Remark : only X10 and ARC are received.  The presence of protocol=arc allows
// to distinguish between them
begin
  ResetBuffer(Buffer);
  Buffer[0] := PACKETLENGTH;
  Buffer[1] := LIGHTING1;  // Type
  // Subtype
  Buffer[2] := GetSubTypeFromString(aMessage.Body.Strings.Values['protocol'],SubTypeArray);
  // Sequence number is fixed value 2
  Buffer[3] := $01;
  // Split the device attribute in housecode and unitcode
  Buffer[4] := Ord(aMessage.Body.Strings.Values['device'][1]);
  Buffer[5] := StrToInt(Copy(aMessage.Body.Strings.Values['device'],2,Length(aMessage.Body.Strings.Values['device'])));
  // Command
  Buffer[6] := GetRFXCode(aMessage.Body.Strings.Values['command'],RFXCommandArray);

  Buffer[7] := $0;
end;


end.
