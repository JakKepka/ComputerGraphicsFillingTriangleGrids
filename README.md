## ComputerGraphicsFillingTriangleGrids
Interpolacja trójkątami powierzchni Beziera. Generowanie oświetlenia przy pomocy modelu Lamberta. Zakrzywianie obrazu przy pomocy mapy wektorów normalnych.  

  Aplikacja pozwala na generowania zdjęcia przy pomocy interpolacji trójkątami, która przyśpiesza obliczenia. Ilość trójkątów za pomoca których modelujemy obraz możemy zmieniać w zależności od potrzeb. Zamodelowane jest oświetlenie obrazu przy pomocy poruszającego sie źródła światła. Obraz jest 3 wymiarowy, przekształcony przez krzywą Beziera 3 stopnia lub krzywą funkcyjną (sin(x+y), gdzie x,y to współrzędne obrazu). Dodatkowo zaimportowane zdjęcie możemy przekształcić/zaburzyć przy pomocy innego zdjęcia (konkretnie mapy wektorów normalnych którym jest to zdjęcie. Obraz możemy również rotować w płaszczyznach.

Wypełnianie siatki trójkątów - podstawowa specyfikacja:

- Wypełniamy każdy trójkąt według poniższych zasad:

1. Algorytm wypełniania wielokątów/trójkątów:
 z sortowaniem kubełkowym krawędzi 
 
2. Kolor wypełniania I:
Składowa rozproszona rmodelu oświetlenia (model Lamberta) + składowa zwierciadlana :
 I = kd*IL*IO*cos(kąt(N,L)) + ks*IL*IO*cosm(kąt(V,R))

3. Kolor wypełnienia punktu wewnątrz trójkąta wyznaczany dokładnie w punkcie interpolując wektory normalne i współrzędną 'z 'do wnętrza' trójkąta
 Do interpolacji używamy współrzędnych barycentrycznych punktu wewnątrz trójkata


4. Zmodyfikowany wektor normalny: N = M*Ntekstury
  Ntekstury - wektor normalny(wersora) odczytany z koloru tekstury (NormalMap) dla całego 'panelu',
  Nx=<-1,+1>, Ny=<-1,+1>, Nz=<-1,+1> (składowa Nz powinna być dodatnia - dlatego Blue=128..255)
  M - macierz przekształcenia (obrotu) dla wektora z tekstury:
    M3x3 = [T, B, Npowierzchni]
     B (wersor binormalny) = Npowierzchni x [0,0,1] (iloczyn wektorowy). Jeśli Npowierzchni=[0,0,1] to B = [0,1,0]
     T (wersor styczny) = B x Npowierzchni (iloczyn wektorowy)
  Npowierzchni - wektor normalny(wersor) odczytany/wyliczony z powierzchni
