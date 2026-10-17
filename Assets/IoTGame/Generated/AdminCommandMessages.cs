// Hand-authored supplement to GameControllerV2.cs — adds AdminCommandRequest and
// AdminCommandResponse, which GameControllerV2.cs lacks because it was generated before they
// were added to Proto/game_controller_v2.proto. Delete this file if you regenerate the stubs.
// Field numbers match the relay's proto definition exactly.
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;

namespace GameController.V2 {

  // proto: message AdminCommandRequest {
  //   string             command   = 1;
  //   string             issued_by = 2;
  //   int64              timestamp = 3;
  //   map<string, float> auxiliary = 4;
  // }
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class AdminCommandRequest : pb::IMessage<AdminCommandRequest>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<AdminCommandRequest> _parser = new pb::MessageParser<AdminCommandRequest>(() => new AdminCommandRequest());
    private pb::UnknownFieldSet _unknownFields;

    // map<string, float> field 4: outer tag=34, entry key tag=10 (string), entry value tag=21 (float/fixed32)
    private static readonly pbc::MapField<string, float>.Codec _map_auxiliary_codec =
        new pbc::MapField<string, float>.Codec(pb::FieldCodec.ForString(10, ""), pb::FieldCodec.ForFloat(21, 0F), 34);
    private readonly pbc::MapField<string, float> auxiliary_ = new pbc::MapField<string, float>();

    public static pb::MessageParser<AdminCommandRequest> Parser { get { return _parser; } }

    pbr::MessageDescriptor pb::IMessage.Descriptor { get { return null; } }

    public AdminCommandRequest() { }

    public AdminCommandRequest(AdminCommandRequest other) : this() {
      command_ = other.command_;
      issuedBy_ = other.issuedBy_;
      timestamp_ = other.timestamp_;
      auxiliary_.Add(other.auxiliary_);
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    public AdminCommandRequest Clone() { return new AdminCommandRequest(this); }

    public const int CommandFieldNumber = 1;
    private string command_ = "";
    public string Command {
      get { return command_; }
      set { command_ = pb::ProtoPreconditions.CheckNotNull(value, "value"); }
    }

    public const int IssuedByFieldNumber = 2;
    private string issuedBy_ = "";
    public string IssuedBy {
      get { return issuedBy_; }
      set { issuedBy_ = pb::ProtoPreconditions.CheckNotNull(value, "value"); }
    }

    public const int TimestampFieldNumber = 3;
    private long timestamp_;
    public long Timestamp {
      get { return timestamp_; }
      set { timestamp_ = value; }
    }

    public const int AuxiliaryFieldNumber = 4;
    public pbc::MapField<string, float> Auxiliary { get { return auxiliary_; } }

    public override bool Equals(object other) { return Equals(other as AdminCommandRequest); }

    public bool Equals(AdminCommandRequest other) {
      if (ReferenceEquals(other, null)) return false;
      if (ReferenceEquals(other, this)) return true;
      if (Command != other.Command) return false;
      if (IssuedBy != other.IssuedBy) return false;
      if (Timestamp != other.Timestamp) return false;
      if (!Auxiliary.Equals(other.Auxiliary)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    public override int GetHashCode() {
      int hash = 1;
      if (Command.Length != 0) hash ^= Command.GetHashCode();
      if (IssuedBy.Length != 0) hash ^= IssuedBy.GetHashCode();
      if (Timestamp != 0L) hash ^= Timestamp.GetHashCode();
      hash ^= Auxiliary.GetHashCode();
      if (_unknownFields != null) hash ^= _unknownFields.GetHashCode();
      return hash;
    }

    public override string ToString() { return $"AdminCommandRequest {{ command={Command}, issued_by={IssuedBy}, timestamp={Timestamp}, auxiliary=[{Auxiliary.Count}] }}"; }

    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (Command.Length != 0) { output.WriteRawTag(10); output.WriteString(Command); }
      if (IssuedBy.Length != 0) { output.WriteRawTag(18); output.WriteString(IssuedBy); }
      if (Timestamp != 0L) { output.WriteRawTag(24); output.WriteInt64(Timestamp); }
      auxiliary_.WriteTo(output, _map_auxiliary_codec);
      if (_unknownFields != null) { _unknownFields.WriteTo(output); }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Command.Length != 0) { output.WriteRawTag(10); output.WriteString(Command); }
      if (IssuedBy.Length != 0) { output.WriteRawTag(18); output.WriteString(IssuedBy); }
      if (Timestamp != 0L) { output.WriteRawTag(24); output.WriteInt64(Timestamp); }
      auxiliary_.WriteTo(ref output, _map_auxiliary_codec);
      if (_unknownFields != null) { _unknownFields.WriteTo(ref output); }
    }
    #endif

    public int CalculateSize() {
      int size = 0;
      if (Command.Length != 0) size += 1 + pb::CodedOutputStream.ComputeStringSize(Command);
      if (IssuedBy.Length != 0) size += 1 + pb::CodedOutputStream.ComputeStringSize(IssuedBy);
      if (Timestamp != 0L) size += 1 + pb::CodedOutputStream.ComputeInt64Size(Timestamp);
      size += auxiliary_.CalculateSize(_map_auxiliary_codec);
      if (_unknownFields != null) size += _unknownFields.CalculateSize();
      return size;
    }

    public void MergeFrom(AdminCommandRequest other) {
      if (other == null) return;
      if (other.Command.Length != 0) Command = other.Command;
      if (other.IssuedBy.Length != 0) IssuedBy = other.IssuedBy;
      if (other.Timestamp != 0L) Timestamp = other.Timestamp;
      auxiliary_.Add(other.auxiliary_);
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        if ((tag & 7) == 4) return;
        switch (tag) {
          default: _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input); break;
          case 10: Command = input.ReadString(); break;
          case 18: IssuedBy = input.ReadString(); break;
          case 24: Timestamp = input.ReadInt64(); break;
          case 34: auxiliary_.AddEntriesFrom(input, _map_auxiliary_codec); break;
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        if ((tag & 7) == 4) return;
        switch (tag) {
          default: _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input); break;
          case 10: Command = input.ReadString(); break;
          case 18: IssuedBy = input.ReadString(); break;
          case 24: Timestamp = input.ReadInt64(); break;
          case 34: auxiliary_.AddEntriesFrom(ref input, _map_auxiliary_codec); break;
        }
      }
    }
    #endif
  }

  // proto: message AdminCommandResponse { bool accepted = 1; string message = 2; }
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class AdminCommandResponse : pb::IMessage<AdminCommandResponse>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<AdminCommandResponse> _parser = new pb::MessageParser<AdminCommandResponse>(() => new AdminCommandResponse());
    private pb::UnknownFieldSet _unknownFields;

    public static pb::MessageParser<AdminCommandResponse> Parser { get { return _parser; } }

    pbr::MessageDescriptor pb::IMessage.Descriptor { get { return null; } }

    public AdminCommandResponse() { }

    public AdminCommandResponse(AdminCommandResponse other) : this() {
      accepted_ = other.accepted_;
      message_ = other.message_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    public AdminCommandResponse Clone() { return new AdminCommandResponse(this); }

    public const int AcceptedFieldNumber = 1;
    private bool accepted_;
    public bool Accepted {
      get { return accepted_; }
      set { accepted_ = value; }
    }

    public const int MessageFieldNumber = 2;
    private string message_ = "";
    public string Message {
      get { return message_; }
      set { message_ = pb::ProtoPreconditions.CheckNotNull(value, "value"); }
    }

    public override bool Equals(object other) { return Equals(other as AdminCommandResponse); }

    public bool Equals(AdminCommandResponse other) {
      if (ReferenceEquals(other, null)) return false;
      if (ReferenceEquals(other, this)) return true;
      if (Accepted != other.Accepted) return false;
      if (Message != other.Message) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    public override int GetHashCode() {
      int hash = 1;
      if (Accepted) hash ^= Accepted.GetHashCode();
      if (Message.Length != 0) hash ^= Message.GetHashCode();
      if (_unknownFields != null) hash ^= _unknownFields.GetHashCode();
      return hash;
    }

    public override string ToString() { return $"AdminCommandResponse {{ accepted={Accepted}, message={Message} }}"; }

    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (Accepted) { output.WriteRawTag(8); output.WriteBool(Accepted); }
      if (Message.Length != 0) { output.WriteRawTag(18); output.WriteString(Message); }
      if (_unknownFields != null) { _unknownFields.WriteTo(output); }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Accepted) { output.WriteRawTag(8); output.WriteBool(Accepted); }
      if (Message.Length != 0) { output.WriteRawTag(18); output.WriteString(Message); }
      if (_unknownFields != null) { _unknownFields.WriteTo(ref output); }
    }
    #endif

    public int CalculateSize() {
      int size = 0;
      if (Accepted) size += 1 + 1;
      if (Message.Length != 0) size += 1 + pb::CodedOutputStream.ComputeStringSize(Message);
      if (_unknownFields != null) size += _unknownFields.CalculateSize();
      return size;
    }

    public void MergeFrom(AdminCommandResponse other) {
      if (other == null) return;
      if (other.Accepted) Accepted = other.Accepted;
      if (other.Message.Length != 0) Message = other.Message;
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        if ((tag & 7) == 4) return;
        switch (tag) {
          default: _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input); break;
          case 8:  Accepted = input.ReadBool(); break;
          case 18: Message = input.ReadString(); break;
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        if ((tag & 7) == 4) return;
        switch (tag) {
          default: _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input); break;
          case 8:  Accepted = input.ReadBool(); break;
          case 18: Message = input.ReadString(); break;
        }
      }
    }
    #endif
  }

}
#endregion Designer generated code
