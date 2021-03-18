if DB_ID('sound_music_DB') is not null
   drop database sound_music_DB
go
create database sound_music_DB
on
(
	name = 'sound_music_DB',
	filename = 'E:\������\�������ֲ�����\WordMusicWinfrom\DB\sound_music_DB.mdf'
)

use sound_music_DB




if OBJECT_ID ('user_info') is not null
drop table user_info
go

--�û���Ϣ��
create table user_info
(
	u_id int identity(10000001,1) unique,--�û�ID
	u_full_name varchar(18) ,--�û��˺� ���ڵ�¼�Զ�����
	u_pwd varchar(18),--�û�����
	u_name varchar(18) ,--�û�����
	register_time date,--����ʱ��
	u_birthday date ,--�û�����
	u_sex varchar(4) check(u_sex in('����','��','Ů')) default '����',--�Ա�
	u_image varchar(100),--�û�Ĭ��ͷ�� ���޸�
	u_signature varchar(80),--�û�ǩ��	
)	
select * from user_info

--select u_full_name from user_info where u_full_name = '�û������˺�'
select * from user_info

--ID���û������������ڡ��������ڡ��Ա�ͷ��ͼƬ��ַ��ǩ��

insert user_info values ('10000001','123456','����','2016-12-24','2000-11-24','��','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ�һ������Ա')
insert user_info values ('10000002','123456','����','2016-06-24','2000-10-24','��','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵڶ�������Ա')
insert user_info values ('10000003','123456','ɣ��','2016-10-24','2000-09-24','��','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ���������Ա')
insert user_info values ('10000004','123456','����','2016-06-24','2000-08-24','Ů','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ��ĸ�����Ա')
insert user_info values ('10000005','123456','ŵ��','2016-10-24','2000-07-24','Ů','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ��������Ա')
insert user_info values ('10000006','123456','����','2016-06-24','2000-06-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ���������Ա')
insert user_info values ('10000007','123456','����','2016-10-24','2000-05-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ��߸�����Ա')
insert user_info values ('10000008','123456','����','2016-06-24','2000-04-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵڰ˸�����Ա')
insert user_info values ('10000009','123456','����','2016-10-24','2000-04-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵھŸ�����Ա')
insert user_info values ('10000010','123456','����','2016-10-24','2000-03-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ�ʮ������Ա')

insert user_info values ('10000011','123456','��','2016-10-24','2000-03-24','����','C:\Users\admin\Desktop\SKYС����Ŀ����\��ջLogo.png','����ֻ�ǵ�ʮ������Ա')

delete from user_info

if OBJECT_ID ('user_like_music') is not null
drop table user_like_music
go

--�û�ϲ�����ֱ�
create table user_like_music    
(
	u_id varchar(8)	not null,
	par_name varchar(18) not null,
	par_id varchar(8) not null,
	s_name varchar(18) not null,
	s_id varchar(8) not null,
)





if OBJECT_ID ('user_song_list') is not null
drop table user_song_list
go

--�û��赥��
create table user_song_list
(
	u_id varchar(8),
	u_name varchar(100),
	
	par_id varchar(100),
	
	song_id int identity(10000001,1),
	song_name varchar(30) ,
	song_collection int ,
	song_sing_u int ,
	song_info varchar(120),
	song_time datetime,
	song_pic varchar(110),
)

update user_song_list set song_collection = 23 where song_id = 10000006 


select * from user_song_list where u_id = 10000001
select Count(*) from user_song_list where u_id = 10000001

select * from user_collection_song_sheet where user_id = 10000001

select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from song_music where song_id = 10000001

select u_id,u_name,par_id,b.song_id,song_name,song_collection,song_sing_u,song_info,song_time,song_pic from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001

delete from user_song_list where song_id = 10000001

delete from user_song_list where song_sing_u = 1

select * from user_song_list where par_id='1001'

--update user_song_list set song_collection = '"+song_collection+"' and song_id = '"+song_id+"'

insert user_song_list values('10000001','����','1002','���ŷ硿�����̴������','8312','12','ϲ���ŷ��С����Ǽǵ��ղ�һ��Ӵ','2019-02-01','SongSheetIMG\32.png')
insert user_song_list values('10000001','����','1002','���ִ����ֽ�ɭ��','999','8','�ұ���΢��ȴ��������','2019-03-04','SongSheetIMG\24.png')
insert user_song_list values('10000001','����','1002','�������㣬����','695','0','����������������������ң�ͬ�ҹ�����ط�������ʳ���˼��̻��ұ�����Ҳ�޺���','2019-06-21','SongSheetIMG\22.png')
insert user_song_list values('10000001','����','1002','���þ������֪','352','0','ɽ��ľ��ľ��֦�����þ������֪','2019-06-21','SongSheetIMG\18.png')
insert user_song_list values('10000002','����','1001','jay����','106','0','','2016-10-10','SongSheetIMG\16.png')
insert user_song_list values('10000002','����','1002','�ϴ���˧���ϴ���˧��','22','0','','2019-10-11','SongSheetIMG\11.png')
insert user_song_list values('10000002','����','1001','����','16','0','','2019-11-11','SongSheetIMG\8.png')
insert user_song_list values('10000001','����','1005','��','15','0','','2019-11-12','SongSheetIMG\27.png')
insert user_song_list values('10000001','����','1006','������ ��ѹ ����','10','0','','2019-01-12','SongSheetIMG\20.png')
insert user_song_list values('10000001','����','1005','�����ź�|�������','4','0','','2019-11-12','SongSheetIMG\31.png')

insert user_song_list values('10000001','����','1003','�˹�|DJ�ϼ�','2','0','','2019-11-12','SongSheetIMG\1.png')
insert user_song_list values('10000003','ɣ��','1001','NBA�����ʹ���ֺϼ�','1','0','','2019-11-12','SongSheetIMG\2.png')
insert user_song_list values('10000003','ɣ��','1001','������������赥','1','0','','2019-11-12','SongSheetIMG\3.png')
insert user_song_list values('10000003','ɣ��','1001','��¹�ϡ����谮��','1','0','','2019-11-12','SongSheetIMG\4.png')
insert user_song_list values('10000003','ɣ��','1004','�Թ��۸������','1','0','','2019-11-12','SongSheetIMG\5.png')
insert user_song_list values('10000003','ɣ��','1009','��������������','0','0','','2019-11-12','SongSheetIMG\6.png')
insert user_song_list values('10000003','ɣ��','1009','��������������','0','0','','2019-11-12','SongSheetIMG\7.png')
insert user_song_list values('10000001','����','1001','�����������ҿ���','0','0','','2019-11-12','SongSheetIMG\12.png')
insert user_song_list values('10000001','����','1003','���������ſ�','0','0','','2019-11-12','SongSheetIMG\9.png')
insert user_song_list values('10000001','����','1008','����Ԫ|����ϼ�','0','0','','2019-11-12','SongSheetIMG\10.png')

insert user_song_list values('10000001','����','1001','�����ױ�|����','0','0','','2019-11-12','SongSheetIMG\13.png')
insert user_song_list values('10000006','����','1005','�һ����㣬ֻ�����Ѳ���','0','0','','2019-11-12','SongSheetIMG\14.png')
insert user_song_list values('10000006','����','1001','�������ȸ���ڵ��߸��϶���','0','0','','2019-11-12','SongSheetIMG\15.png')
insert user_song_list values('10000006','����','1005','˯���ŵ�ʱ��������','0','0','','2019-11-12','SongSheetIMG\17.png')
insert user_song_list values('10000006','����','1005','����������','0','0','','2019-11-12','SongSheetIMG\19.png')
insert user_song_list values('10000006','����','1002','̨�����߹���������ɫ','0','0','','2019-11-12','SongSheetIMG\21.png')
insert user_song_list values('10000006','����','1001','������Max|С�̹�','0','0','','2019-11-12','SongSheetIMG\23.png')
insert user_song_list values('10000006','����','1006','��̨���֡�СĪ','0','0','','2019-11-12','SongSheetIMG\25.png')
insert user_song_list values('10000001','����','1006','�����ߣ���������?','0','0','','2019-11-12','SongSheetIMG\28.png')
insert user_song_list values('10000004','����','1004','ֻ�����Сè��|��','0','0','','2019-11-12','SongSheetIMG\29.png')

insert user_song_list values('10000004','����','1009','��̸����������û��','0','0','','2019-11-12','SongSheetIMG\30.png')
insert user_song_list values('10000004','����','1008','�����Ѷ�|��ɫ','0','0','','2019-11-12','SongSheetIMG\33.png')
insert user_song_list values('10000004','����','1001','AWSL|��','0','0','','2019-11-12','SongSheetIMG\34.png')
insert user_song_list values('10000005','ŵ��','1001','��һ����|����','0','0','','2019-11-12','SongSheetIMG\35.png')
insert user_song_list values('10000005','ŵ��','1009','ݮ����','0','0','','2019-11-12','SongSheetIMG\36.png')
insert user_song_list values('10000005','ŵ��','1009','����1��','0','0','','2019-11-12','SongSheetIMG\37.png')
insert user_song_list values('10000007','����','1009','����2��','0','0','','2019-11-12','SongSheetIMG\38.png')
insert user_song_list values('10000008','����','1009','����3��','0','0','','2019-11-12','SongSheetIMG\39.png')
insert user_song_list values('10000009','����','1001','��ү|ֿ��','0','0','','2019-11-12','SongSheetIMG\40.png')
insert user_song_list values('10000010','����','1001','С��ʹ','0','0','','2019-11-12','SongSheetIMG\26.png')


insert user_song_list values('�û�ID','�û�����','�赥����','�赥����','0','0','','2019-11-12','SongSheetIMG\26.png')


select *from user_song_list

select u_id,u_name,par_id,b.song_id,song_name,song_collection,song_sing_u,song_info,song_time,song_pic from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001

select * from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001
delete from user_collection_song_sheet where song_id = 10000002

--�û���ע�赥��
if OBJECT_ID ('user_collection_song_sheet') is not null
drop table user_collection_song_sheet
go
create table user_collection_song_sheet
(
	--�û���ID
	user_id varchar(8) not null,
	--�赥��ID
	song_id varchar(8) not null,	
)
--�鿴����ղظ赥
select * from user_collection_song_sheet where user_id = 10000001
--�鿴����赥���������ղ���
select * from user_collection_song_sheet where song_id = 10000001

select Count(*) from user_collection_song_sheet where user_id = '"+user_id+"' and song_id = '"+song_id+"'

insert user_collection_song_sheet values (10000001,10000005)
insert user_collection_song_sheet values (10000001,10000006)
insert user_collection_song_sheet values (10000001,10000007)
insert user_collection_song_sheet values (10000001,10000012)
insert user_collection_song_sheet values (10000001,10000013)
insert user_collection_song_sheet values (10000001,10000014)
insert user_collection_song_sheet values (10000001,10000015)
insert user_collection_song_sheet values (10000001,10000016)
insert user_collection_song_sheet values (10000001,10000017)

insert user_collection_song_sheet values (10000002,10000001)
insert user_collection_song_sheet values (10000003,10000001)
insert user_collection_song_sheet values (10000004,10000001)
insert user_collection_song_sheet values (10000005,10000001)
insert user_collection_song_sheet values (10000006,10000001)
insert user_collection_song_sheet values (10000007,10000001)
insert user_collection_song_sheet values (10000008,10000001)
insert user_collection_song_sheet values (10000009,10000001)
insert user_collection_song_sheet values (10000010,10000001)

insert user_collection_song_sheet values (10000002,10000002)
insert user_collection_song_sheet values (10000003,10000002)
insert user_collection_song_sheet values (10000004,10000002)
insert user_collection_song_sheet values (10000005,10000002)
insert user_collection_song_sheet values (10000006,10000002)
insert user_collection_song_sheet values (10000007,10000002)
insert user_collection_song_sheet values (10000008,10000002)
insert user_collection_song_sheet values (10000009,10000002)



--�û���ע��And�û���˿��
if OBJECT_ID ('user_followandfans') is not null
drop table user_followandfans
go
create table user_followandfans
(
	--����ע�û���ID
	fuser_id varchar(8) not null,
	--��ע�û���ID
	user_id varchar(8) not null,
	
)
--�鿴�Լ���ע��
select * from user_followandfans where user_id = 10000001
--�鿴��˿��
select * from user_followandfans where fuser_id = 10000001

insert user_followandfans values (10000001,10000002)
insert user_followandfans values (10000001,10000003)
insert user_followandfans values (10000001,10000004)
insert user_followandfans values (10000001,10000005)
insert user_followandfans values (10000001,10000006)
insert user_followandfans values (10000001,10000007)
insert user_followandfans values (10000001,10000008)
insert user_followandfans values (10000001,10000009)
insert user_followandfans values (10000001,10000010)

insert user_followandfans values (10000002,10000001)
insert user_followandfans values (10000002,10000005)
insert user_followandfans values (10000002,10000006)
insert user_followandfans values (10000002,10000007)
insert user_followandfans values (10000002,10000008)
insert user_followandfans values (10000002,10000009)
insert user_followandfans values (10000002,10000010)







if OBJECT_ID ('song_music') is not null
drop table song_music
go
--�赥���ֱ�
--ͨ��ר����ID�͸�����ר����ID���Կ��ٵ��ҵ�ר���еĵ������֣�������赥��
--select * from ���� where a_id = ר��ID and cd_id = ר���ڵ���ID
create table song_music
(
	song_id varchar(8) not null,--�赥��ID 
	songone_id varchar(8) not null,--�赥�ڵ�����ID
	a_id varchar(8) not null,--ר����ID
	a_name varchar(18) not null,--ר��������
	m_singer varchar(18) not null,--ר���ĸ���
	par_lyric varchar(100) not null,--�赥���
	music_address varchar(100) not null,--�赥����
	song_time varchar(12) not null,--�赥ʱ��
	cd_id varchar(8) not null,--ר����ID
	cd_name varchar(100) not null,--��������

)
--��ѯ��ǰ���ŵĸ���
select * from song_music where song_id = 10000001 and songone_id = 1
select * from song_music where song_id = 10000001

select Count(*) from song_music where song_id = 10000001

select * from song_music 

delete from song_music where song_id = 10000001

select * from upload_music where a_id = (select top 1 a_id from song_music where song_id = 10000001) and cd_id = (select top 1 cd_id from song_music where song_id = 10000001)
--insert song_music values('�赥��ID','�赥�ڵ�����ID','ר����ID',ר������,ר����������,���λ��,����λ��,����ʱ��,'ר���ڸ�����ID','��������')
select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from song_music where song_id = 10000001
insert song_music values ('10000001','1','10000001','���걨��','����','SingSonglrc\���� - ���׹���.lrc','SingSong\���� - ���׹���.flac','04:09','1','���׹���')
insert song_music values ('10000001','2','10000001','���걨��','����','SingSonglrc\���� - �ϹŶ�.lrc','SingSong\���� - �ϹŶ�.flac','04:04','2','�ϹŶ�')
insert song_music values ('10000001','3','10000001','���걨��','����','SingSonglrc\���� - ���һ�.lrc','SingSong\���� - ���һ�.flac','04:02','3','���һ�')
insert song_music values ('10000001','4','10000001','���걨��','����','SingSonglrc\���� - �ȵ��̻�����.lrc','SingSong\���� - �ȵ��̻�����.flac','03:01','4','�ȵ��̻�����')
insert song_music values ('10000001','5','10000001','���걨��','����','SingSonglrc\���� - ��ָһ�Ӽ�.lrc','SingSong\���� - ��ָһ�Ӽ�.flac','04:47','5','��ָһ�Ӽ�')
insert song_music values ('10000001','6','10000001','���걨��','����','SingSonglrc\���� - ������.lrc','SingSong\���� - ������.flac','03:24','6','������')
insert song_music values ('10000001','7','10000001','���걨��','����','SingSonglrc\���� - ��Ϧ.lrc','SingSong\���� - ��Ϧ.flac','03:53','7','��Ϧ')
insert song_music values ('10000001','8','10000001','���걨��','����','SingSonglrc\���� - ɽˮ֮��.lrc','SingSong\���� - ɽˮ֮��.flac','04:36','8','ɽˮ֮��')
insert song_music values ('10000001','9','10000001','���걨��','����','SingSonglrc\���� - ��ͩ��.lrc','SingSong\���� - ��ͩ��.flac','04:37','9','��ͩ��')
insert song_music values ('10000001','10','10000001','���걨��','����','SingSonglrc\���� - ����ԼԼ.lrc','SingSong\���� - ����ԼԼ.flac','03:23','10','����ԼԼ')
insert song_music values ('10000001','11','10000002','����Բ�ȥ','����','SingSonglrc\���� - ����֮��.lrc','SingSong\���� - ����֮��.flac','04:33','1','����֮��')
insert song_music values ('10000001','12','10000003','��Ļ','����','SingSonglrc\���� - ��Ļ.lrc','SingSong\���� - ��Ļ.flac','04:00','1','��Ļ')

insert song_music values ('10000002','1','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - �ֿ�.lrc','SingSong\Ѧ֮ǫ - �ֿ�.flac','04:10','1','�ֿ�')
insert song_music values ('10000002','2','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - ����.lrc','SingSong\Ѧ֮ǫ - ����.flac','03:54','2','����')
insert song_music values ('10000002','3','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - Ħ���¥.lrc','SingSong\Ѧ֮ǫ - Ħ���¥.flac','03:50','3','Ħ���¥')
insert song_music values ('10000002','4','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - �������뿪�˱���������.lrc','SingSong\Ѧ֮ǫ - �������뿪�˱���������.flac','04:28','4','�������뿪�˱���������')
insert song_music values ('10000002','5','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - ���.lrc','SingSong\Ѧ֮ǫ - ���.flac','04:08','5','���')
insert song_music values ('10000002','6','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - �ư�.lrc','SingSong\Ѧ֮ǫ - �ư�.flac','04:21','6','�ư�')
insert song_music values ('10000002','7','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - ���.lrc','SingSong\Ѧ֮ǫ - ���.flac','04:15','7','���')
insert song_music values ('10000002','8','20000001','�ֿ�','Ѧ֮ǫ','SingSonglrc\Ѧ֮ǫ - ���޼ɵ�.lrc','SingSong\Ѧ֮ǫ - ���޼ɵ�.flac','04:19','8','���޼ɵ�')








if OBJECT_ID ('music') is not null
drop table music
go
--���ֱ�
create table music
(
	cd_id varchar(8) not null unique,
	cd_name varchar(18) not null,
	cd_album varchar(18) not null,
	cd_singer varchar(18) not null,
	cd_lyric varchar(1000) not null,
	cd_link varchar(100) not null,
)

if OBJECT_ID ('album') is not null
drop table album
go
--ר���� ����Щר�� �ֱ���˭����ר��
create table album
(
	s_id varchar(8) not null,
	s_name varchar(18) not null,
	a_id  varchar(8) not null,
	a_name varchar(18) not null,
	a_time varchar(18) not null,
	a_details varchar(500) not null,
	a_bigpic varchar(100) not null,
	a_smlpic varchar(100) not null,
	a_smlpictx varchar(100) not null,
)
select * from album
 
 select a_smlpictx from album where a_id =10000001
 
select * from album where s_name = '����';

insert album values('s_100001','����','10000001','���걨��','2012-12-24','��������������һ����ʮ���������골���Լ���������潻�ģ�Ҳ��һ����������������������������˼�������������Խ�������ʶ�������̯��һ�����������İ��棺������������ζ��ů�Ĺ��¡���Ӱר����������ȱ���ڡ���Ѹ��֡�������ͷ���Լ�����˯����á� ��������','SingSongpic\SSP10001.png','SingSongpic\SSP20001.png','SingSongpic\SMP10001.png')
insert album values('s_100001','����','10000002','����Բ�ȥ','2012-12-24','���ڰ������ĵ������������ĸ�������˵����ô�࣬Ҳ������ô�࣬�������Σ�','SingSongpic\SSP10002.png','SingSongpic\SSP20002.png','SingSongpic\SMP10002.png')
insert album values('s_100001','����','10000003','��Ļ','2012-12-24','ʱ��һ��δ���������˴Ρ�ƮȻһ�����յù�����ԡ�����������������Ļ��������Եĸ����ϣ����ơ����������ꡢ���ˣ�Ȼ������ȴ����������糲�֮��ʱ�Ȼ��ֹ�����±����µ��������Ҳ����ˮ�档','SingSongpic\SSP10003.png','SingSongpic\SSP20003.png','SingSongpic\SMP10003.png')
insert album values('s_100002','Ѧ֮ǫ','20000001','�ֿ�','2012-11-24','��Ϊ�ֿ������޺��Ӱ������Цŭ���Ⱥ���Ƿ�ƽ�˾�����Ъ˹��صķ�񣿲���ƽӹ��ƽӹ��������ͨ����ͨ���ֿ����֣�ֻ�Ǳ��������Ϊ�����ϵġ��֡�','SingSongpic\SSP10004.png','SingSongpic\SSP20004.png','SingSongpic\SMP10004.png')
insert album values('s_100002','Ѧ֮ǫ','20000002','��ʿ','2012-11-24','���Ǳ�Ӱ��ȥ �·�Ҳ���� ������ñ �����¾�ȫ�����ѡ��ġ��������������Ѧ֮ǫ','SingSongpic\SSP10005.png','SingSongpic\SSP20005.png','SingSongpic\SMP10005.png')
insert album values('s_100002','Ѧ֮ǫ','20000003','��','2012-11-24','2012�껪������Ȧ�����ڴ���Ƭ��Ʒȫ�ܳ������Ѧ֮ǫ�������� ���ն������մӻ�������������ʵ�����þ�','SingSongpic\SSP10006.png','SingSongpic\SMP10001.png','SingSongpic\SMP10006.png')
insert album values('s_100002','Ѧ֮ǫ','20000004','һ��','2012-11-24','һ����ң���������һ�����������һ�롣һ��������ĵĳ����¸�������һ������Ƿ��ܹ���֪����','SingSongpic\SSP10007.png','SingSongpic\SSP20007.png','SingSongpic\SMP10007.png')



if OBJECT_ID ('upload_music') is not null
drop table upload_music
go
--�ϴ����ֱ�
create table upload_music
(
	u_id varchar(8) not null,
	u_name varchar(18) not null,
	upload_time date not null,
	a_id varchar(8) not null,
	a_name varchar(18) not null,
	m_singer varchar(18) not null,
	par_id varchar(100) not null,
	par_name varchar(100) not null,
	par_lyric varchar(100) not null,
	music_address varchar(100) not null,
	song_time varchar(12) not null,
	cd_id varchar(8) not null,
	cd_name varchar(100) not null,

)

select * from upload_music

select Count(*) from upload_music where a_id = '10000002'

select Count(*)from (select distinct a_name from upload_music where m_singer = '����') As Temp 

select top 3 *  from upload_music where a_id = '10000001' 

select * from upload_music where a_id = '10000001' and cd_id = '2'

select TOP 3 a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music 

select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music where a_id = 10000001 and cd_id = 1

insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ���׹���.lrc','SingSong\���� - ���׹���.flac','04:09','1','���׹���')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - �ϹŶ�.lrc','SingSong\���� - �ϹŶ�.flac','04:04','2','�ϹŶ�')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ���һ�.lrc','SingSong\���� - ���һ�.flac','04:02','3','���һ�')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - �ȵ��̻�����.lrc','SingSong\���� - �ȵ��̻�����.flac','03:01','4','�ȵ��̻�����')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ��ָһ�Ӽ�.lrc','SingSong\���� - ��ָһ�Ӽ�.flac','04:47','5','��ָһ�Ӽ�')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1002','�ŷ�','SingSonglrc\���� - ������.lrc','SingSong\���� - ������.flac','03:24','6','������')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ��Ϧ.lrc','SingSong\���� - ��Ϧ.flac','03:53','7','��Ϧ')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ɽˮ֮��.lrc','SingSong\���� - ɽˮ֮��.flac','04:36','8','ɽˮ֮��')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ��ͩ��.lrc','SingSong\���� - ��ͩ��.flac','04:37','9','��ͩ��')
insert upload_music values ('20000001','������','2012-12-24','10000001','���걨��','����','1001','�ִ�','SingSonglrc\���� - ����ԼԼ.lrc','SingSong\���� - ����ԼԼ.flac','03:23','10','����ԼԼ')

insert upload_music values ('20000001','������','2012-12-24','10000002','����Բ�ȥ','����','1001','�ִ�','SingSonglrc\���� - ����֮��.lrc','SingSong\���� - ����֮��.flac','04:33','1','����֮��')

insert upload_music values ('20000001','������','2012-12-24','10000003','��Ļ','����','1002','�ŷ�','SingSonglrc\���� - ��Ļ.lrc','SingSong\���� - ��Ļ.flac','04:00','1','��Ļ')

insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - �ֿ�.lrc','SingSong\Ѧ֮ǫ - �ֿ�.flac','04:10','1','�ֿ�')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ����.lrc','SingSong\Ѧ֮ǫ - ����.flac','03:54','2','����')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - Ħ���¥.lrc','SingSong\Ѧ֮ǫ - Ħ���¥.flac','03:50','3','Ħ���¥')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - �������뿪�˱���������.lrc','SingSong\Ѧ֮ǫ - �������뿪�˱���������.flac','04:28','4','�������뿪�˱���������')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ���.lrc','SingSong\Ѧ֮ǫ - ���.flac','04:08','5','���')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - �ư�.lrc','SingSong\Ѧ֮ǫ - �ư�.flac','04:21','6','�ư�')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ���.lrc','SingSong\Ѧ֮ǫ - ���.flac','04:15','7','���')
insert upload_music values ('20000002','��ǫ','2012-11-24','20000001','�ֿ�','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ���޼ɵ�.lrc','SingSong\Ѧ֮ǫ - ���޼ɵ�.flac','04:19','8','���޼ɵ�')

insert upload_music values ('20000002','��ǫ','2012-11-24','20000002','��ʿ','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ��Ա.lrc','SingSong\Ѧ֮ǫ - ��Ա.flac','04:21','1','��Ա')

insert upload_music values ('20000002','��ǫ','2012-11-24','20000003','��','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - ����������.lrc','SingSong\Ѧ֮ǫ - ����������.flac','03:36','1','����������')

insert upload_music values ('20000002','��ǫ','2012-11-24','20000004','һ��','Ѧ֮ǫ','1001','�ִ�','SingSonglrc\Ѧ֮ǫ - һ��.lrc','SingSong\Ѧ֮ǫ - һ��.flac','04:46','1','һ��')


select cd_name,m_singer,a_name,song_time,par_lyric,music_address from upload_music



if OBJECT_ID ('partition') is not null
drop table partition
go
--����(������������)
create table partition
(
	par_id varchar(100) not null unique,
	par_name varchar(100) not null,
)

select par_name from partition where par_id = '1002'

insert partition values('1001','�ִ�')
insert partition values('1002','�ŷ�')
insert partition values('1003','ҡ��')
insert partition values('1004','����')
insert partition values('1005','�������')
insert partition values('1006','������')
insert partition values('1007','Ӱ��ԭ��')
insert partition values('1008','ACG')
insert partition values('1009','����')


if OBJECT_ID ('singer') is not null
drop table singer
go
--���ֱ�
create table singer
(
	u_id varchar(8) not null unique,
	s_id varchar(8) not null unique,
	s_type varchar(10) not null,
	s_sex varchar(8)not null,
	s_name varchar(18) not null,
	cd_u int not null,
	album_u int not null,
	mv_u int not null,
	s_pic varchar(400)not null,
	s_info varchar(400),
)

select * from singer where s_name='����'
select * from singer where s_sex='�и���'

insert singer values ('20000001','s_100001','����','�и���','����','0','0','0','SingerIMG\����.png','��Һã��������ԣ��Ժ�Ҳ���Ҷ����ա�')
insert singer values ('20000002','s_100002','����','�и���','Ѧ֮ǫ','0','0','0','SingerIMG\Ѧ֮ǫ.png','�񾭲���')
insert singer values ('20000003','s_100003','�ձ�','�и���','�׽�����','0','0','0','SingerIMG\�׽�����.png','��Һ�')
insert singer values ('20000004','s_100004','ŷ��','Ů����','Billie Eilish','0','0','0','SingerIMG\Billie Eilish.png','bad gay')
insert singer values ('20000005','s_100005','����','�и���','������','0','0','0','SingerIMG\������.png','��Һ����ǻ���')
insert singer values ('20000006','s_100006','����','�ֶӸ���','S.H.E','0','0','0','SingerIMG\S.H.E.png','')
insert singer values ('20000007','s_100007','����','�и���','Sou','0','0','0','SingerIMG\Sou.png','')
insert singer values ('20000008','s_100008','ŷ��','�и���','TheFatRat','0','0','0','SingerIMG\TheFatRat.png','')
insert singer values ('20000009','s_100009','�ձ�','�и���','�ܤ��Τ�','0','0','0','SingerIMG\�ܤ��Τ��.png','')
insert singer values ('20000010','s_100010','�ձ�','�и���','�ޤդޤ�','0','0','0','SingerIMG\�ޤդޤ�.png','')
insert singer values ('20000011','s_100011','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000012','s_100012','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000013','s_100013','����','Ů����','�����','0','0','0','SingerIMG\�����.png','')
insert singer values ('20000014','s_100014','����','Ů����','���μ�','0','0','0','SingerIMG\���μ�.png','')
insert singer values ('20000015','s_100015','�ձ�','�и���','��������','0','0','0','SingerIMG\��������.png','')
insert singer values ('20000016','s_100016','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000017','s_100017','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000018','s_100018','����','Ů����','����Ī','0','0','0','SingerIMG\����Ī.png','')
insert singer values ('20000019','s_100019','�ձ�','�и���','���濵��','0','0','0','SingerIMG\���濵��.png','')
insert singer values ('20000020','s_100020','����','Ů����','����','0','0','0','SingerIMG\����.png','')
insert singer values ('20000021','s_100021','�ձ�','�и���','�����˾','0','0','0','SingerIMG\�����˾.png','')
insert singer values ('20000022','s_100022','����','Ů����','����','0','0','0','SingerIMG\����.png','')
insert singer values ('20000023','s_100023','����','Ů����','Aki����','0','0','0','SingerIMG\Aki����.png','���֣��Ժ������ʸе������������� ���������������ơ���������־����������־���������ġ��ȡ�')
insert singer values ('20000024','s_100024','����','Ů����','��ʦ��','0','0','0','SingerIMG\��ʦ��.png','')
insert singer values ('20000025','s_100025','�ձ�','�и���','���ｫ��','0','0','0','SingerIMG\���ｫ��.png','')
insert singer values ('20000026','s_100026','�ձ�','�и���','��Ұ�v��','0','0','0','SingerIMG\��Ұ�v��.png','')
insert singer values ('20000027','s_100027','����','�и���','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000028','s_100028','����','�и���','���ٺ�','0','0','0','SingerIMG\���ٺ�.png','')
insert singer values ('20000029','s_100029','����','Ů����','��ӽ��','0','0','0','SingerIMG\��ӽ��.png','')
insert singer values ('20000030','s_100030','����','�и���','��־��','0','0','0','SingerIMG\��־��.png','')
insert singer values ('20000031','s_100031','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000032','s_100032','����','�и���','ë����','0','0','0','SingerIMG\ë����.png','')
insert singer values ('20000033','s_100033','�ձ�','�и���','DAISHI DANCE','0','0','0','SingerIMG\DAISHI DANCE.png','')
insert singer values ('20000034','s_100034','����','�и���','�Źǽ���','0','0','0','SingerIMG\�Źǽ���.png','')
insert singer values ('20000035','s_100035','����','�и���','����MarBlue','0','0','0','SingerIMG\����MarBlue.png','')
insert singer values ('20000036','s_100036','����','�и���','���Գ�','0','0','0','SingerIMG\���Գ�.png','')
insert singer values ('20000037','s_100037','����','Ů����','˫��','0','0','0','SingerIMG\˫��.png','')
insert singer values ('20000038','s_100038','����','Ů����','̷άά','0','0','0','SingerIMG\̷άά.png','')
insert singer values ('20000039','s_100039','����','Ů����','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000040','s_100040','����','�и���','������','0','0','0','SingerIMG\������.png','')
insert singer values ('20000041','s_100041','����','�и���','С��','0','0','0','SingerIMG\С��.png','')
insert singer values ('20000042','s_100042','����','�и���','�����','0','0','0','SingerIMG\�����.png','')
insert singer values ('20000043','s_100043','����','�и���','����Բ','0','0','0','SingerIMG\����Բ.png','')
insert singer values ('20000044','s_100044','ŷ��','Ů����','DJ Okawari','0','0','0','SingerIMG\DJ Okawari.png','')
insert singer values ('20000045','s_100045','����','�и���','en','0','0','0','SingerIMG\en.png','')
insert singer values ('20000046','s_100046','����','�и���','����','0','0','0','SingerIMG\����.png','')
insert singer values ('20000047','s_100047','����','�и���','����Ryan.B','0','0','0','SingerIMG\����Ryan.B.png','')
insert singer values ('20000048','s_100048','�ձ�','�и���','��Ұ��֮','0','0','0','SingerIMG\��Ұ��֮.png','')
insert singer values ('20000049','s_100049','����','�и���','�ܰغ�','0','0','0','SingerIMG\�ܰغ�.png','')
insert singer values ('20000050','s_100050','����','�и���','�ܽ���','0','0','0','SingerIMG\�ܽ���.png','')

select * from singer where s_name = '�ܽ���'
select s_name from singer

select * from singer

if OBJECT_ID ('upload_music') is not null

drop table upload_music
go
--�ϴ����ֱ�
create table upload_music
(
	u_id varchar(8) not null unique,
	u_name varchar(18) not null,
	upload_time varchar(18) not null,
	a_id varchar(8) not null unique,
	a_name varchar(18) not null,
	m_singer varchar(18) not null,
	par_id varchar(8) not null unique,
	par_name varchar(18) not null,
	par_lyric varchar(100) not null,
	music_address varchar(100) not null,

)


 