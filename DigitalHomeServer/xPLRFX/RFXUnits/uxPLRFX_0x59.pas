(***********************************************************)
(* xPLRFX                                                  *)
(* part of Digital Home Server project                     *)
(* http://www.digitalhomeserver.net                        *)
(* info@digitalhomeserver.net                              *)
(***********************************************************)
unit uxPLRFX_0x59;

interface

Uses uxPLRFXConst, u_xPL_Message, u_xpl_common, uxPLRFXMessages;

procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);

implementation

Uses SysUtils;

(*

Type $59 - Current sensors

Buffer[0] = packetlength = $0D;
Buffer[1] = packettype
Buffer[2] = subtype
Buffer[3] = seqnbr
Buffer[4] = id1
Buffer[5] = id2
Buffer[6] = count
Buffer[7] = ch1_high
Buffer[8] = ch1_low
Buffer[9] = ch2_high
Buffer[10] = ch2_low
Buffer[11] = ch3_high
Buffer[12] = ch3_low
Buffer[13] = battery_level:4/rssi:4

Test strings :

0D59010F860004001D0000000049

xPL Schema

sensor.basic
{
  device=elec1_1 0x<hex sensor id>
  type=current
  current=<ampere>
}

sensor.basic
{
  device=elec1_2 0x<hex sensor id>
  type=current
  current=<ampere>
}

sensor.basic
{
  device=elec1_3 0x<hex sensor id>
  type=current
  current=<ampere>
}

sensor.basic
{
  device=elec1 0x<hex sensor id>
  type=battery
  current=0-100
}

*)

const
  // Type
  CURRENT  = $59;

  // Subtype
  ELEC1  = $01;

var
  SubtypeArray : array[1..1] of TRFXSubTypeRec =
    ((SubType : ELEC1; SubTypeString : 'elec1'));


procedure RFX2xPL(Buffer : BytesArray; xPLMessages : TxPLRFXMessages);
var
  DeviceID, DeviceID1, DeviceID2, DeviceID3 : String;
  SubType : Byte;
  Ampere1, Ampere2, Ampere3 : Extended;
  BatteryLevel : Integer;
  xPLMessage : TxPLMessage;
begin
  SubType := Buffer[2];
  DeviceID := GetSubTypeString(SubType,SubTypeArray)+IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2);
  DeviceID1 := GetSubTypeString(SubType,SubTypeArray)+'_1'+IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2);
  DeviceID2 := GetSubTypeString(SubType,SubTypeArray)+'_2'+IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2);
  DeviceID3 := GetSubTypeString(SubType,SubTypeArray)+'_3'+IntToHex(Buffer[4],2)+IntToHex(Buffer[5],2);
  Ampere1 := ((Buffer[7] shl 8) + Buffer[8]) / 10;
  Ampere2 := ((Buffer[9] shl 8) + Buffer[10]) / 10;
  Ampere3 := ((Buffer[11] shl 8) + Buffer[12]) / 10;
  if (Buffer[13] and $0F) = 0 then  // zero out rssi
    BatteryLevel := 0
  else
    BatteryLevel := 100;

  // Create sensor.basic messages
  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'sensor.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+DeviceID1);
  xPLMessage.Body.AddKeyValue('current='+FloatToStr(Ampere1));
  xPLMessage.Body.AddKeyValue('type=current');
  xPLMessages.Add(xPLMessage.RawXPL);
  xPLMessage.Free;

  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'sensor.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+DeviceID2);
  xPLMessage.Body.AddKeyValue('current='+FloatToStr(Ampere2));
  xPLMessage.Body.AddKeyValue('type=current');
  xPLMessages.Add(xPLMessage.RawXPL);
  xPLMessage.Free;

  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'sensor.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+DeviceID3);
  xPLMessage.Body.AddKeyValue('current='+FloatToStr(Ampere3));
  xPLMessage.Body.AddKeyValue('type=current');
  xPLMessages.Add(xPLMessage.RawXPL);
  xPLMessage.Free;

  xPLMessage := TxPLMessage.Create(nil);
  xPLMessage.schema.RawxPL := 'sensor.basic';
  xPLMessage.MessageType := trig;
  xPLMessage.source.RawxPL := XPLSOURCE;
  xPLMessage.target.IsGeneric := True;
  xPLMessage.Body.AddKeyValue('device='+DeviceID);
  xPLMessage.Body.AddKeyValue('current='+IntToStr(BatteryLevel));
  xPLMessage.Body.AddKeyValue('type=battery');
  xPLMessages.Add(xPLMessage.RawXPL);
  xPLMessage.Free;

end;


end.
