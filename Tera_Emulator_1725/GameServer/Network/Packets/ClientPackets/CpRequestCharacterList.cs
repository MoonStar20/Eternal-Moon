using Communication.Logic;

namespace Network.Client
{
    public class CpRequestCharacterList : ARecvPacket
    {
        public override void Read()
        {
            AccountLogic.GetPlayerList(Connection);
        }

        public override void Process()
        {
            AccountLogic.GetPlayerList(Connection);
        }
    }
}