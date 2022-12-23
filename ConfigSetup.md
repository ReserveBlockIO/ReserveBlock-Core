Summary:
The point of this paper is to outline the config file setup and features it has.
1.	Port (default is 3338)
	+ a. Type int
	+ b. Ex: Port=3338.
	+ c. This is the port for all p2p functions. This should remain as 3338.
  
2.	APIPort (default is 7292)
	+ a.	Type int
	+ b.	Ex: APIPort=7292
	+ c.	This is the port to call the API. This may be changed to whatever you want.

3.	WalletPassword (default is null)
	+ a.	Type string
	+ b.	Ex: WalletPassword=SomePassword1234!
	+ c.	This is a password that will lock all functions in wallet and require you input a password to unlock wallet functions.

4.	AlwaysRequireWalletPassword (default is false)
	+ a.	Type boolean
	+ b.	Ex: AlwaysRequireWalletPassword=true
	+ c.	If set to true you will be required to input password before every function call. 

5.	APIPassword (default is null)
	+ a.	Type string
	+ b.	Ex: APIPassword=SomePassword1234!
	+ c.	This is a password set to lock down control of the API. This password must be inputted to call any API function.
	+ d.    You will need to call UnlockWallet to unlock the API with this password.

6.	AlwaysRequireAPIPassword (default is false)
	+ a.	Type boolean
	+ b.	Ex: AlwaysRequireAPIPassword=true
	+ c.	If set to true then you must include the password in all API request.

7.	APICallURL (default is null)
	+ a.	Type string
	+ b.	Ex: APICallURL=https://mycallbackurl.com
	+ c.	This URL is used to send incoming transactions to an outside URL. This is something used for like incoming deposits or other notification. services.

8.	WalletUnlockTime (default is 15)
	+ a.	Type int
	+ b.	Ex: WalletUnlockTime=5
	+ c.	This is the amount of time once a password has been entered the wallet will remain unlocked and not need password again. If any of the RequirePasswords above are set to true, then it will override this setting and require the password everytime. 

9.	ChainCheckPoint (default is false)
	+ a.	Type Boolean
	+ b.	Ex: ChainCheckPoint=true
	+ c.	This is a feature that will turn on chain check points. What this will do is create a FULL copy of the chains state and back it up and create a checkpoint you can revert back to in the event your current structure becomes corrupted. 

10.	ChainCheckPointInterval (default is 12)
	+ a.	Type int
	+ b.	Ex: ChainCheckPointInterval=4
	+ c.	This interval is in hours and means it will run the checkpoint every X amount of hours with 1 being the smallest allowed.

11.	ChainCheckPointRetain (default is 2)
	+ a.	Type int
	+ b.	Ex: ChainCheckPointRetain=4
	+ c.	Retain is what will determine how many backups to keep before it will begin to remove the oldest backup to make room for new one.

12.	ChainCheckPointLocation (default is default path)
	+ a.	Type string
	+ b.	Ex: ChainCheckPointLocation=C:\somelocation\otherthan\default\
	+ c.	If the default path is not wanted you can setup a new location for them to go to.

13.	APICallURLLogging (default is false)
	+ a.	Type Boolean
	+ b.	Ex: APICallURLLogging=true 
	+ c.	This will debug the API call URL in the event there is an issue with TX’s being sent to your URL. This is recommended to be turned on during the initial testing of your URL.

14.	ValidatorAddress (default is null)
	+ a.	Type string
	+ b.	Ex: ValidatorAddress=RBcHyS2AF4Z4jgzTLZNBVLvtxQ6MtB2vUN
	+ c.	You can input a default validator address to instantly start validating. Please note you must also import the wallet address through the launch commands. Ex: ReserveBlockCore.exe privKey=MyPrivateKey.

15.	ValidatorName (default is new guid)
	+ a.	Type string
	+ b.	Ex: ValidatorName=MyValidatorRBX1
	+ c.	This is the name for the above validator that will be used. Once validator has been imported once then these settings won’t do anything and can be removed or left there. 

16.	NFTTimeout (default is 15 secs)
	+ a.	Type int
	+ b.	Ex: NFTTimeout=5
	+ c.	This will control the timeout for processing an incoming NFT 

17.	PasswordClearTime (default is 10 mins)
	+ a.	Type int
	+ b.	Ex: PasswordClearTime=5
	+ c.	This will control the clear time for an encrypted wallets password

18.	AutoDownloadNFTAsset (default is true)
	+ a.	Type Boolean
	+ b.	Ex: AutoDownloadNFTAsset=false
	+ c.	This will control whether or not an NFT's asset is automatically downloaded

19.	IgnoreIncomingNFTs (default is false)
	+ a.	Type Boolean
	+ b.	Ex: IgnoreIncomingNFTs=false
	+ c.	This will control whether or not incoming NFTs are processed or just added as a TX record

20.	RejectAssetExtensionTypes (default is a List of rejected assets)
	+ a.	Type List<string>
	+ b.	Ex: RejectAssetExtensionTypes=exe,zip,pdf... (ensure there are no spaces between types)
	+ c.	This will add extension types to the already defined list and will reject any NFT assets with these known extension types


21.	AllowedExtensionsTypes (default is null)
	+ a.	Type List<string>
	+ b.	Ex: AllowedExtensionsTypes=pdf,doc,xls
	+ c.	This will remove extension types to the already defined list and will allow any NFT assets with these known extension types to be downloaded
