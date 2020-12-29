# PacketFFXI
A first attempt to get into a topaz ffxi server with a packet only client. So far it can connect using a already set up username and select a already created charater.  
It can send packets to the server that are at the moment manualy created.  
Asis it will just walk back and forth in a line
### TODO's  
[x]Account creation if server says invalid info--Will try to create a account with the username and password provided  
[x]Charater creation if no charaters are on the account or use a lower slot if slot selected is empty  
[x]Decompress the incoming packets so they can be parsed for correct responces  
[]Parse incoming packets for data
