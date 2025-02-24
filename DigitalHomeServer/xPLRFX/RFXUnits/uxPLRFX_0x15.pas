(***********************************************************)
(* xPLRFX                                                  *)
(* part of Digital Home Server project                     *)
(* http://www.digitalhomeserver.net                        *)
(* info@digitalhomeserver.net                              *)
(***********************************************************)
unit uxPLRFX_0x15;

interface

Uses uxPLRFXConst, u_xPL_Message, u_xpl_common, uxPLRFXMessages;

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);

implementation

Uses SysUtils;

(*

Type $15 - Lighting6 - Blyss

Buffer[0] = packetlength = $0B;
Buffer[1] = packettype
Buffer[2] = subtype
Buffer[3] = seqnbr
Buffer[4] = id1
Buffer[5] = id2
Buffer[6] = groupcode
Buffer[7] = unitcode
Buffer[8] = cmnd
Buffer[9] = cmndseqnbr
Buffer[10] = rfu
Buffer[11] = filler:4/rssi:4

xPL Schema

x10.basic
{
  device=<housecode[unitcode]>
  command=on|off|all_lights_on|all_lights_off
  protocol=blyss
}

*)

const
  // Packet length
  PACKETLENGTH  = $0B;

  // Type
  LIGHTING6     = $15;

  // Subtype
  BLYSS         = $00;

  // Commands
  COMMAND_OFF      = 'off';
  COMMAND_ON       = 'on';
  COMMAND_GROUPOFF = 'all_lights_off';
  COMMAND_GROUPON  = 'all_lights_on';

var
  // Lookup table for commands
  RFXCommandArray : array[1..4] of TRFXCommandRec =
    ((RFXCode : $00; xPLCommand : COMMAND_ON),
     (RFXCode : $01; xPLCommand : COMMAND_OFF),
     (RFXCode : $02; xPLCommand : COMMAND_GROUPON),
     (RFXCode : $03; xPLCommand : COMMAND_GROUPOFF));

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
var
  ID : String;
  HouseCode : Byte;
  UnitCode : Byte;
  Command : String;
  xPLMessage : TxPLMessage;
begin
  Id := IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2);
  HouseCode := Buffer[4];
  UnitCode := Buffer[5];
  Command := GetxPLCommand(Buffer[8],RFXCommandArray);

  // Create x10.basic message
  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'x10.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+Id+Chr(HouseCode)+IntToStr(UnitCode));
  xPLMessage.Body.AddKeyValue('command='+Command);
  xPLMessage.Body.AddKeyValue('protocol=blyss');
  xPLMessages.Add(xPLMessage.RawXPL);
end;

procedure xPL2RFX(aMessage : TxPLMessage; var Buffer : BytesArray);
begin
  ResetBuffer(Buffer);
  Buffer[0] := PACKETLENGTH;
  Buffer[1] := LIGHTING6;  // Type
  // Subtype
  Buffer[2] := BLYSS;
  // Split the device attribute in id1, id2, housecode and unitcode
  Buffer[4] := StrToInt('$'+Copy(aMessage.Body.Strings.Values['device'],1,2));
  Buffer[5] := StrToInt('$'+Copy(aMessage.Body.Strings.Values['device'],3,2));
  Buffer[6] := Ord(aMessage.Body.Strings.Values['device'][5]);
  Buffer[7] := StrToInt(aMessage.Body.Strings.Values['device'][6]);
  // Command
  Buffer[8] := GetRFXCode(aMessage.Body.Strings.Values['command'],RFXCommandArray);

  Buffer[9] := $0;
end;

end.

